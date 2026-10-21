using System;
using System.Collections.Generic;
using System.Text;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenccFmmsegLib
{
    /// <summary>
    /// Provides a managed wrapper for the OpenCC + FMMSEG C API for Chinese text conversion and segmentation.
    /// </summary>
    /// <remarks>
    /// This class manages the native OpenCC instance and provides methods for text conversion and language checks.
    /// </remarks>
    public sealed class OpenccFmmseg : IDisposable
    {
        // Pre-encoded config bytes for common configurations
        private static readonly Dictionary<string, byte[]> EncodedConfigCache =
            new Dictionary<string, byte[]>(capacity: 24, comparer: StringComparer.Ordinal);

        private IntPtr _openccInstance;
        private bool _disposed;

        // Static constructor to pre-encode canonical config strings for efficient native interop.
        // Single-owner: the set of configs comes from OpenccConfig enum.
        static OpenccFmmseg()
        {
            foreach (OpenccConfig cfg in Enum.GetValues(typeof(OpenccConfig)))
            {
                var canonical = cfg.ToCanonicalName(); // always lowercase canonical
                if (EncodedConfigCache.ContainsKey(canonical))
                    continue; // Defensive: should never happen unless enum has duplicates

                EncodedConfigCache[canonical] = Utf8Z.FromString(canonical);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenccFmmseg"/> class.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the native OpenCC instance cannot be created.</exception>
        public OpenccFmmseg()
        {
            _openccInstance = OpenccFmmsegNative.opencc_new();
            if (_openccInstance != IntPtr.Zero) return;
            var lastError = LastError();
            throw new InvalidOperationException($"Failed to initialize OpenCC. {lastError}".Trim());
        }

        /// <summary>Creates an immutable converter using built-in dictionaries plus custom mappings.</summary>
        /// <param name="customDictionarySpecifications">
        /// Ordered specifications to apply at initialization. An empty collection uses the built-in dictionaries.
        /// </param>
        /// <remarks>
        /// Requires native <c>opencc_new_custom</c> (v0.11.5 or later); Seal slots require v0.13.0 or later.
        /// All bundled native runtimes are v0.13.1 and support all 25 dictionary slots.
        /// The native constructor copies all mappings before returning. Later changes to caller collections
        /// do not affect this instance. All existing conversion APIs use the resulting dictionaries.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The specifications collection is null.</exception>
        /// <exception cref="ArgumentException">The collection contains a null specification.</exception>
        /// <exception cref="InvalidOperationException">Native initialization fails, including for an unsupported slot.</exception>
        /// <example>
        /// <code>
        /// var specifications = new[] {
        ///     new CustomDictSpec(DictSlot.STPhrases, CustomDictMode.Append,
        ///         new Dictionary&lt;string, string&gt; { ["汉字转换"] = "自訂轉換" })
        /// };
        /// using (var converter = new OpenccFmmseg(specifications))
        /// {
        ///     string result = converter.Convert("汉字转换", "s2t"); // 自訂轉換
        /// }
        /// </code>
        /// </example>
        public OpenccFmmseg(IEnumerable<CustomDictSpec> customDictionarySpecifications)
        {
            using (var buffers = new CustomDictionaryMarshaller(customDictionarySpecifications))
                _openccInstance = buffers.CreateInstance();
        }

        /// <summary>Normalizes mapped CJK Compatibility Ideographs using the native built-in table.</summary>
        /// <param name="text">Input text; null or empty returns an empty string.</param>
        /// <returns>Normalized text, preserving characters without a native compatibility mapping.</returns>
        /// <remarks>
        /// Requires native v0.12.0 or later and a live converter instance. This optional preprocessing step
        /// is not general-purpose NFC, NFD, NFKC, or NFKD normalization. It does not run automatically in Convert.
        /// Use <see cref="NormalizeCompatExtended"/> to also apply the curated extended compatibility mappings.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The converter has been disposed, including for empty input.</exception>
        /// <exception cref="ArgumentException">The input contains an embedded NUL character.</exception>
        /// <exception cref="InvalidOperationException">The native operation fails.</exception>
        public string NormalizeCompat(string text)
        {
            return TransformText(text, TextTransform.NormalizeCompat);
        }

        /// <summary>Normalizes CJK Compatibility Ideographs and applies native curated Unicode compatibility mappings.</summary>
        /// <param name="text">Input text; null or empty returns an empty string.</param>
        /// <returns>Normalized text, preserving characters without an applicable native mapping.</returns>
        /// <remarks>
        /// Requires native v0.12.0 or later and a live converter instance. This optional preprocessing step
        /// extends <see cref="NormalizeCompat"/>; it is not general-purpose NFC, NFD, NFKC, or NFKD normalization.
        /// Recommended order: normalize, Convert, then optionally <see cref="DeTofu"/> for display fallback.
        /// Existing conversion methods do not enable normalization automatically.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The converter has been disposed, including for empty input.</exception>
        /// <exception cref="ArgumentException">The input contains an embedded NUL character.</exception>
        /// <exception cref="InvalidOperationException">The native operation fails.</exception>
        public string NormalizeCompatExtended(string text)
        {
            return TransformText(text, TextTransform.NormalizeCompatExtended);
        }

        /// <summary>Applies native built-in fallback mappings for display compatibility at an inclusive extension threshold.</summary>
        /// <param name="text">Input text; null or empty returns an empty string for a valid level.</param>
        /// <param name="level">The earliest CJK extension eligible for replacement, including supported later extensions.</param>
        /// <returns>Display fallback text. Characters without an applicable mapping remain unchanged.</returns>
        /// <remarks>
        /// Requires native v0.12.0 or later and a live converter instance. DeTofu is an optional display fallback,
        /// not Chinese linguistic conversion. Apply it after Convert when the target font lacks rare characters.
        /// <see cref="DeTofuLevel.ExtB"/> is the broadest threshold. No question marks or replacement glyphs
        /// are substituted for unmapped characters. Existing conversion behavior remains unchanged.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The converter has been disposed, including for empty input.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The level is undefined, including for null or empty input.</exception>
        /// <exception cref="ArgumentException">The input contains an embedded NUL character.</exception>
        /// <exception cref="InvalidOperationException">The native operation fails.</exception>
        public string DeTofu(string text, DeTofuLevel level)
        {
            return TransformText(text, TextTransform.DeTofu, level);
        }

        private enum TextTransform
        {
            NormalizeCompat,
            NormalizeCompatExtended,
            DeTofu
        }

        private string TransformText(string text, TextTransform operation, DeTofuLevel level = DeTofuLevel.ExtB)
        {
            ThrowIfDisposed();
            if (operation == TextTransform.DeTofu && !Enum.IsDefined(typeof(DeTofuLevel), level))
                throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown DeTofu threshold.");
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.IndexOf('\0') >= 0)
                throw new ArgumentException("Input must not contain embedded NUL characters.", nameof(text));

            byte[] inputBuffer = null;
            var output = IntPtr.Zero;
            try
            {
                Utf8Z.Rent(text, out inputBuffer, out _);
                switch (operation)
                {
                    case TextTransform.NormalizeCompat:
                        output = OpenccFmmsegNative.opencc_normalize_compat(_openccInstance, inputBuffer);
                        break;
                    case TextTransform.NormalizeCompatExtended:
                        output = OpenccFmmsegNative.opencc_normalize_compat_extended(_openccInstance, inputBuffer);
                        break;
                    case TextTransform.DeTofu:
                        output = OpenccFmmsegNative.opencc_detofu(_openccInstance, inputBuffer, (uint)level);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
                }

                // transform_core clears the thread-local error on success, but may return allocated
                // error text on failure. Check immediately, even when the returned pointer is nonzero.
                var lastError = LastError();
                if (output == IntPtr.Zero || (lastError.Length != 0 && lastError != "No error"))
                    throw new InvalidOperationException($"{operation} failed. {lastError}".Trim());
                return Utf8Z.ToStringZ(output);
            }
            finally
            {
                try
                {
                    if (output != IntPtr.Zero) OpenccFmmsegNative.opencc_string_free(output);
                }
                finally
                {
                    Utf8Z.Return(inputBuffer);
                }
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="OpenccFmmseg"/> instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the instance and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Dispose managed state (managed objects).
                // Currently, OpenccFmmseg does not have any managed IDisposable resources.
            }

            if (_openccInstance != IntPtr.Zero)
            {
                OpenccFmmsegNative.opencc_delete(_openccInstance);
                _openccInstance = IntPtr.Zero;
            }

            _disposed = true;
        }

        /// <summary>
        /// Finalizer to ensure native resources are released.
        /// </summary>
        ~OpenccFmmseg()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the numeric ABI version of the underlying native OpenCC-FMMSEG library.
        /// </summary>
        /// <remarks>
        /// The ABI number represents the native binary interface version and is used
        /// to ensure compatibility between this managed wrapper and the native library.
        /// <para/>
        /// A change in ABI number indicates a breaking change at the native interface level.
        /// </remarks>
        /// <returns>
        /// An integer representing the native ABI version.
        /// </returns>
        public static int GetNativeAbiNumber()
        {
            return (int)OpenccFmmsegNative.opencc_abi_number();
        }

        /// <summary>
        /// Gets the version string of the underlying native OpenCC-FMMSEG library.
        /// </summary>
        /// <remarks>
        /// The returned value is a semantic version string in the form <c>x.y.z</c>
        /// (for example, <c>0.8.5</c>), identifying the native library build.
        /// <para/>
        /// This value is intended for diagnostics, logging, and display purposes.
        /// </remarks>
        /// <returns>
        /// A semantic version string (<c>x.y.z</c>) reported by the native library.
        /// </returns>
        public static string GetNativeVersionString()
        {
            return Utf8Z.ToStringZ(OpenccFmmsegNative.opencc_version_string());
        }

        /// <summary>
        /// Converts the input Chinese text using the specified OpenCC configuration.
        /// </summary>
        /// <param name="input">
        /// The UTF-16 .NET input string to convert.
        /// If <paramref name="input"/> is <c>null</c> or empty, this method returns an empty string.
        /// </param>
        /// <param name="config">
        /// Canonical OpenCC configuration name (for example, <c>"s2t"</c>, <c>"s2twp"</c>, <c>"t2hk"</c>).
        /// Case-insensitive, but otherwise validated strictly.
        /// </param>
        /// <param name="punctuation">
        /// <c>true</c> to convert punctuation as well; otherwise <c>false</c>.
        /// </param>
        /// <returns>
        /// The converted string on success.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This overload validates <paramref name="config"/> using the managed configuration parser
        /// and throws for unsupported names instead of silently substituting a fallback configuration.
        /// </para>
        /// <para>
        /// Prefer <see cref="Convert(string,OpenccConfig,bool)"/> when possible for stronger typing
        /// and compile-time discoverability.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="config"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="config"/> is not a supported canonical OpenCC configuration name.
        /// </exception>
        public string Convert(string input, string config, bool punctuation = false)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(input))
                return string.Empty;
            var canonical = OpenccConfigExtensions.Parse(config).ToCanonicalName();
            return ConvertInternal(input, canonical, punctuation);
        }

        /// <summary>
        /// Converts the input Chinese text using a typed OpenCC configuration.
        /// </summary>
        /// <param name="input">
        /// The UTF-16 .NET input string to convert.
        /// If <paramref name="input"/> is <c>null</c> or empty, this method returns an empty string.
        /// </param>
        /// <param name="configId">
        /// The typed OpenCC configuration identifier.
        /// </param>
        /// <param name="punctuation">
        /// <c>true</c> to convert punctuation as well; otherwise <c>false</c>.
        /// </param>
        /// <returns>
        /// The converted string on success.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This overload is the recommended entry point for most callers because it is
        /// strongly typed, self-documenting, and avoids string-based configuration errors.
        /// </para>
        /// <para>
        /// Internally, the configuration is canonicalized and forwarded to the native layer.
        /// Native-side validation still applies.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        public string Convert(string input, OpenccConfig configId, bool punctuation = false)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Canonicalize once and forward to the core executor.
            var canonical = configId.ToCanonicalName();
            return ConvertInternal(input, canonical, punctuation);
        }

        /// <summary>
        /// Core native-backed conversion routine using a canonical OpenCC configuration.
        /// </summary>
        /// <param name="input">
        /// The UTF-16 .NET input string to convert.
        /// This value must be non-null and non-empty.
        /// </param>
        /// <param name="canonicalConfig">
        /// A canonical OpenCC configuration name in lowercase form
        /// (for example, <c>"s2t"</c>, <c>"s2twp"</c>).
        /// <para>
        /// This value <b>must</b> already be validated and canonicalized by the caller.
        /// No further validation is performed at this level.
        /// </para>
        /// </param>
        /// <param name="punctuation">
        /// <c>true</c> to convert punctuation as well; otherwise <c>false</c>.
        /// </param>
        /// <returns>
        /// The converted string returned by the native OpenCC library.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is an internal execution primitive and is not intended to be called
        /// directly by library consumers.
        /// </para>
        /// <para>
        /// Configuration validation and fallback logic are handled at higher layers
        /// (for example, in <see cref="Convert(string,OpenccConfig,bool)"/> or
        /// <see cref="Convert(string,string,bool)"/>).
        /// </para>
        /// <para>
        /// Memory management:
        /// <list type="bullet">
        /// <item>
        /// Input text is encoded to UTF-8 into a rented buffer and passed to the native API.
        /// </item>
        /// <item>
        /// The native output string is always released via
        /// <c>opencc_string_free</c> after decoding.
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// If the canonical configuration is missing from the internal cache, an
        /// <see cref="InvalidOperationException"/> is thrown, indicating an internal
        /// consistency error.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <paramref name="canonicalConfig"/> is not found in the internal
        /// configuration cache. This indicates an internal programming error.
        /// </exception>
        private string ConvertInternal(string input, string canonicalConfig, bool punctuation)
        {
            byte[] inputBuffer = null;

            try
            {
                // Rent + encode + '\0'
                Utf8Z.Rent(input, out inputBuffer, out _);

                // 🔹 encoded config MUST exist (canonical guaranteed)
                if (!EncodedConfigCache.TryGetValue(canonicalConfig, out var configBytes))
                {
                    // This should never happen unless internal tables are corrupted
                    throw new InvalidOperationException(
                        $"Internal error: canonical OpenCC config '{canonicalConfig}' not found.");
                }

                var output = OpenccFmmsegNative.opencc_convert(
                    _openccInstance,
                    inputBuffer,
                    configBytes,
                    punctuation);

                try
                {
                    return Utf8Z.ToStringZ(output);
                }
                finally
                {
                    if (output != IntPtr.Zero)
                        OpenccFmmsegNative.opencc_string_free(output);
                }
            }
            finally
            {
                Utf8Z.Return(inputBuffer);
            }
        }

        /// <summary>
        /// Converts the input Chinese text using a numeric OpenCC configuration ID
        /// (<c>opencc_config_t</c>).
        /// </summary>
        /// <param name="input">
        /// The UTF-16 .NET input string to convert.
        /// If <paramref name="input"/> is <c>null</c> or empty, this method returns an empty string.
        /// </param>
        /// <param name="configNum">
        /// The numeric OpenCC configuration ID (<c>opencc_config_t</c>), for example
        /// <c>OPENCC_CONFIG_S2TWP</c>.
        /// </param>
        /// <param name="punctuation">
        /// <c>true</c> to convert punctuation as well; otherwise <c>false</c>.
        /// </param>
        /// <returns>
        /// The converted string on success.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This overload is an advanced API intended for interop scenarios (e.g., bindings, CLI parsing,
        /// or cross-language integration) where the configuration is already represented as a numeric ID.
        /// </para>
        /// <para>
        /// <b>Configuration validation (gating)</b><br/>
        /// The managed layer intentionally does not validate <paramref name="configNum"/>.
        /// Validation is performed by the native layer to ensure consistent behavior across languages.
        /// If <paramref name="configNum"/> is invalid, the native side returns an allocated UTF-8
        /// error message string such as <c>"Invalid config: &lt;value&gt;"</c> and also stores it as
        /// the last error.
        /// </para>
        /// <para>
        /// <b>Memory ownership</b><br/>
        /// The returned native string is always released via <c>opencc_string_free</c> after decoding.
        /// </para>
        /// <para>
        /// <b>Return value notes</b><br/>
        /// This method returns an empty string only when <paramref name="input"/> is null/empty,
        /// or if the native call returns <see cref="IntPtr.Zero"/> (for example, due to OOM).
        /// In the invalid-config case, the returned string contains the native error message.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        /// <inheritdoc cref="ConvertCfg(string,int,bool)"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string ConvertCfg(string input, int configNum, bool punctuation = false)
        {
            ThrowIfDisposed();
            return string.IsNullOrEmpty(input) ? string.Empty : ConvertCfgInternal(input, configNum, punctuation);
        }

        /// <summary>
        /// Converts the input Chinese text using a typed OpenCC configuration ID
        /// (<see cref="OpenccConfig"/>).
        /// </summary>
        /// <param name="input">
        /// The UTF-16 .NET input string to convert.
        /// If <paramref name="input"/> is <c>null</c> or empty, this method returns an empty string.
        /// </param>
        /// <param name="configId">
        /// The typed OpenCC configuration identifier.
        /// </param>
        /// <param name="punctuation">
        /// <c>true</c> to convert punctuation as well; otherwise <c>false</c>.
        /// </param>
        /// <returns>
        /// The converted string on success.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This overload is intended for advanced scenarios where the conversion configuration
        /// is already represented as an enum ID.
        /// </para>
        /// <para>
        /// Internally, this forwards to the native numeric-config API.
        /// Native-side validation still applies for consistency with other bindings.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        public string ConvertCfg(string input, OpenccConfig configId, bool punctuation = false)
        {
            ThrowIfDisposed();
            return string.IsNullOrEmpty(input) ? string.Empty : ConvertCfgInternal(input, (int)configId, punctuation);
        }

        /// <summary>
        /// Core native-backed conversion routine using a numeric OpenCC config ID
        /// (<c>opencc_config_t</c>).
        /// </summary>
        /// <param name="input">
        /// The UTF-16 .NET input string to convert. Must be non-null and non-empty.
        /// </param>
        /// <param name="configNum">
        /// Numeric OpenCC configuration ID (<c>opencc_config_t</c>).
        /// Validation is performed by the native layer.
        /// </param>
        /// <param name="punctuation">
        /// <c>true</c> to convert punctuation; otherwise <c>false</c>.
        /// </param>
        /// <returns>
        /// The decoded native result string. In the invalid-config case, this will be the
        /// native error string (for example, <c>"Invalid config: &lt;value&gt;"</c>).
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is an internal execution primitive intended for advanced overloads.
        /// It does not validate <paramref name="configNum"/> and relies on native gating to
        /// ensure cross-language consistency.
        /// </para>
        /// <para>
        /// The native output pointer is always freed via <c>opencc_string_free</c>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        private string ConvertCfgInternal(string input, int configNum, bool punctuation)
        {
            ThrowIfDisposed();

            byte[] inputBuffer = null;
            try
            {
                // Rent + encode + '\0'
                Utf8Z.Rent(input, out inputBuffer, out _);

                var output = OpenccFmmsegNative.opencc_convert_cfg(
                    _openccInstance,
                    inputBuffer,
                    configNum,
                    punctuation);

                try
                {
                    return Utf8Z.ToStringZ(output);
                }
                finally
                {
                    if (output != IntPtr.Zero)
                        OpenccFmmsegNative.opencc_string_free(output);
                }
            }
            finally
            {
                Utf8Z.Return(inputBuffer);
            }
        }

        /// <summary>
        /// Converts text using a numeric OpenCC configuration into a caller-provided UTF-8 buffer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is an advanced API for callers that specifically need UTF-8 output in a
        /// caller-provided managed buffer. For ordinary conversion, prefer
        /// <see cref="Convert(string,OpenccConfig,bool)"/> or <see cref="ConvertCfg(string,OpenccConfig,bool)"/>.
        /// </para>
        /// <para>
        /// The implementation uses the explicit input-length native API
        /// (<c>opencc_convert_cfg_mem_len</c>), so the UTF-8 input buffer does not require a trailing NUL byte.
        /// The size-query protocol performs native conversion once to determine the required output size and
        /// again to write the result; use this method for its buffer contract, not as a performance optimization.
        /// </para>
        /// </remarks>
        public unsafe bool TryConvertCfgToUtf8(
            string input,
            int configId,
            bool punctuation,
            Span<byte> destination,
            out int requiredBytes)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(input))
            {
                requiredBytes = 1;
                if (destination.Length < 1)
                    return false;

                destination[0] = 0;
                return true;
            }

            var inputByteCount = Encoding.UTF8.GetByteCount(input);
            byte[] inputBuffer = null;

            try
            {
                inputBuffer = ArrayPool<byte>.Shared.Rent(inputByteCount);
                var written = Encoding.UTF8.GetBytes(input, 0, input.Length, inputBuffer, 0);

                var okQuery = OpenccFmmsegNative.opencc_convert_cfg_mem_len(
                    _openccInstance,
                    inputBuffer,
                    (UIntPtr)written,
                    configId,
                    punctuation,
                    IntPtr.Zero,
                    (UIntPtr)0,
                    out var required);

                requiredBytes = ToInt32Checked(required);

                if (!okQuery || destination.Length < requiredBytes)
                    return false;

                fixed (byte* dstPtr = destination)
                {
                    var ok = OpenccFmmsegNative.opencc_convert_cfg_mem_len(
                        _openccInstance,
                        inputBuffer,
                        (UIntPtr)written,
                        configId,
                        punctuation,
                        (IntPtr)dstPtr,
                        (UIntPtr)destination.Length,
                        out var required2);

                    requiredBytes = ToInt32Checked(required2);
                    return ok;
                }
            }
            finally
            {
                if (inputBuffer != null)
                    ArrayPool<byte>.Shared.Return(inputBuffer);
            }
        }

        /// <summary>
        /// Legacy name for <see cref="TryConvertCfgToUtf8"/>.
        /// </summary>
        /// <remarks>
        /// Deprecated because the explicit-length native implementation is now the standard implementation detail
        /// of <see cref="TryConvertCfgToUtf8"/> and should not be exposed in the managed API name.
        /// </remarks>
        [Obsolete(
            "Use TryConvertCfgToUtf8 instead. The managed API now uses the explicit-length native path internally.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool TryConvertCfgToUtf8Into(
            string input,
            int configId,
            bool punctuation,
            Span<byte> destination,
            out int requiredBytes)
        {
            return TryConvertCfgToUtf8(input, configId, punctuation, destination, out requiredBytes);
        }

        private static int ToInt32Checked(UIntPtr value)
        {
            var v = value.ToUInt64();
            if (v > int.MaxValue)
                throw new OverflowException("Required buffer size exceeds Int32.MaxValue.");
            return (int)v;
        }

        /// <summary>
        /// Converts text using a numeric OpenCC configuration and returns a newly
        /// allocated UTF-8 byte array including a trailing NUL terminator.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method allocates the output buffer in managed memory. Use it only when a
        /// NUL-terminated managed UTF-8 byte array is specifically required. For ordinary
        /// conversion, prefer <see cref="Convert(string,OpenccConfig,bool)"/> or
        /// <see cref="ConvertCfg(string,OpenccConfig,bool)"/>.
        /// </para>
        /// <para>
        /// The returned byte array represents a valid UTF-8 C-style string and
        /// always includes the trailing NUL byte (<c>'\0'</c>).
        /// </para>
        /// <para>
        /// The underlying explicit-length buffer API uses a size query followed by an output pass, so this
        /// method should not be selected as a performance optimization. If caller-owned buffer
        /// placement is required, use
        /// <see cref="TryConvertCfgToUtf8(string,int,bool,System.Span{byte},out int)"/>.
        /// </para>
        /// </remarks>
        /// <param name="input">
        /// Input text as a managed .NET <see cref="string"/> (UTF-16).
        /// If <paramref name="input"/> is <see langword="null"/> or empty,
        /// the result is a single NUL byte.
        /// </param>
        /// <param name="configId">
        /// Numeric OpenCC configuration identifier (for example <c>OPENCC_CONFIG_S2TWP</c>).
        /// </param>
        /// <param name="punctuation">
        /// Whether to convert punctuation. Some configurations may ignore this flag.
        /// </param>
        /// <returns>
        /// A newly allocated UTF-8 byte array containing the converted text,
        /// terminated with a NUL byte.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the conversion fails. The exception message contains the
        /// native error returned by <see cref="LastError"/> when available.
        /// </exception>
        public byte[] ConvertCfgToUtf8Z(string input, int configId, bool punctuation = false)
        {
            ThrowIfDisposed();

            // First call with empty destination to get required bytes
            var dummy = Span<byte>.Empty;
            _ = TryConvertCfgToUtf8(input, configId, punctuation, dummy, out var required);

            // required includes '\0'
            var buf = new byte[required];

            if (TryConvertCfgToUtf8(input, configId, punctuation, buf, out _)) return buf;
            // Match the library style: surface native last error
            var err = LastError();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(err) ? "OpenccFmmseg conversion failed." : err);
        }

        /// <summary>
        /// Converts text using a numeric OpenCC configuration and returns a newly
        /// allocated UTF-8 byte array including a trailing NUL terminator.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is an advanced API for interop or native bridging when the caller specifically
        /// requires a managed, NUL-terminated UTF-8 buffer instead of a managed
        /// <see cref="string"/>. For ordinary conversion, prefer
        /// <see cref="Convert(string,OpenccConfig,bool)"/> or
        /// <see cref="ConvertCfg(string,OpenccConfig,bool)"/>.
        /// </para>
        /// <para>
        /// Internally, this method uses the explicit input-length native buffer API
        /// (<c>opencc_convert_cfg_mem_len</c>) and allocates the result entirely in
        /// managed memory.
        /// </para>
        /// <para>
        /// The underlying buffer API uses a size query followed by an output pass. That
        /// two-pass work can be slower than the normal conversion APIs, so use this method for
        /// its output format and ownership behavior, not as a performance optimization.
        /// </para>
        /// <para>
        /// The returned byte array always represents a valid UTF-8 C-style string and
        /// includes a trailing NUL byte (<c>0x00</c>).
        /// </para>
        /// <para>
        /// For callers that want to reuse their own destination buffer, prefer
        /// <see cref="TryConvertCfgToUtf8Into(string,int,bool,System.Span{byte},out int)"/>.
        /// </para>
        /// </remarks>
        /// <param name="input">
        /// Input text as a managed .NET <see cref="string"/> (UTF-16).
        /// If <paramref name="input"/> is <see langword="null"/> or empty, the result is
        /// a single NUL byte.
        /// </param>
        /// <param name="configId">
        /// Numeric OpenCC configuration identifier (for example <c>OPENCC_CONFIG_S2TWP</c>).
        /// </param>
        /// <param name="punctuation">
        /// Whether to convert punctuation. Some configurations may ignore this flag.
        /// </param>
        /// <returns>
        /// A newly allocated UTF-8 byte array containing the converted text,
        /// terminated with a trailing NUL byte.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the conversion fails. The exception message contains the
        /// native error returned by <see cref="LastError"/> when available.
        /// </exception>
        [Obsolete(
            "Use ConvertCfgToUtf8Z instead. The managed API now uses the explicit-length native path internally.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public byte[] ConvertCfgMemLenToUtf8Z(string input, int configId, bool punctuation = false)
        {
            return ConvertCfgToUtf8Z(input, configId, punctuation);
        }

        /// <summary>
        /// Converts text using a numeric OpenCC configuration and returns the result as a managed string
        /// through the explicit-length, buffer-based native API.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a specialized API for callers that intentionally need the explicit-length
        /// buffer path (<c>opencc_convert_cfg_mem_len</c>). For ordinary managed-string
        /// conversion, prefer <see cref="Convert(string,OpenccConfig,bool)"/> or
        /// <see cref="ConvertCfg(string,OpenccConfig,bool)"/>.
        /// </para>
        /// <para>
        /// The buffer protocol performs native conversion once to query the required output size
        /// and again to produce the output. A retry can repeat that work. This overhead can make
        /// this method slower than the normal conversion APIs despite avoiding input NUL
        /// termination and native output-string allocation.
        /// </para>
        /// <para>
        /// If conversion fails, this method throws an <see cref="InvalidOperationException"/>
        /// containing the native error message from <see cref="LastError"/>.
        /// </para>
        /// </remarks>
        /// <param name="input">
        /// Input text as a managed .NET <see cref="string"/> (UTF-16).
        /// If <paramref name="input"/> is <see langword="null"/> or empty, an empty string is returned.
        /// </param>
        /// <param name="configId">
        /// Numeric OpenCC configuration identifier (for example <c>OPENCC_CONFIG_S2TWP</c>).
        /// </param>
        /// <param name="punctuation">
        /// Whether to convert punctuation. Some configurations may ignore this flag.
        /// </param>
        /// <returns>
        /// The converted text as a managed <see cref="string"/>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if native conversion fails.
        /// </exception>
        [Obsolete(
            "Use ConvertCfg instead. MemLen is a native implementation detail and should not be exposed in the managed API name.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string ConvertCfgMemLen(string input, int configId, bool punctuation = false)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // First: try stack buffer (fast path)
            Span<byte> stackBuffer = stackalloc byte[1024];

            if (TryConvertCfgToUtf8(input, configId, punctuation, stackBuffer, out var requiredBytes))
            {
                return requiredBytes <= 1
                    ? string.Empty
                    : Utf8Z.ToString(stackBuffer.Slice(0, requiredBytes - 1));
            }

            if (requiredBytes <= 0)
                throw new InvalidOperationException(LastError());

            // Fallback: pooled buffer
            var rented = ArrayPool<byte>.Shared.Rent(requiredBytes);
            try
            {
                var buffer = rented.AsSpan(0, requiredBytes);

                if (!TryConvertCfgToUtf8(input, configId, punctuation, buffer, out requiredBytes))
                    throw new InvalidOperationException(LastError());

                return requiredBytes <= 1
                    ? string.Empty
                    : Utf8Z.ToString(buffer.Slice(0, requiredBytes - 1));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Checks if the input string is Chinese text using the OpenCC language check.
        /// </summary>
        /// <param name="input">The input string to check.</param>
        /// <returns>An integer indicating the result of the check
        /// (implementation-defined by OpenCC. 1 - Traditional, 2 - Simplified, 0 - Others).
        /// </returns>
        public int ZhoCheck(string input)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(input))
                return 0;

            byte[] buffer = null;
            try
            {
                Utf8Z.Rent(input, out buffer, out _);
                return OpenccFmmsegNative.opencc_zho_check(_openccInstance, buffer);
            }
            finally
            {
                Utf8Z.Return(buffer);
            }
        }

        /// <summary>
        /// Gets the last error message recorded by the native OpenCC library for the calling thread.
        /// </summary>
        /// <returns>
        /// The last error message for the calling thread, <c>"No error"</c> if no error is recorded,
        /// or an empty string if the native function cannot allocate the returned message.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Native last-error state is scoped to the calling thread, not to a specific
        /// <see cref="OpenccFmmseg"/> instance. Call this method on the same thread immediately
        /// after a native operation fails.
        /// </para>
        /// <para>
        /// Calls made on other threads do not overwrite this thread's stored error. A later OpenCC
        /// call on the same thread may replace or clear it.
        /// </para>
        /// <para>
        /// Each invocation retrieves an independently allocated native string, which this method
        /// converts to a managed string and releases before returning.
        /// </para>
        /// </remarks>
        public static string LastError()
        {
            var cLastError = OpenccFmmsegNative.opencc_last_error();
            if (cLastError == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Utf8Z.ToStringZ(cLastError);
            }
            finally
            {
                OpenccFmmsegNative.opencc_error_free(cLastError);
            }
        }

        /// <summary>
        /// Attempts to map a canonical OpenCC config name to a typed <see cref="OpenccConfig"/>
        /// using the native C API (<c>opencc_config_name_to_id</c>).
        /// </summary>
        /// <param name="name">
        /// Canonical configuration name (for example, <c>"s2t"</c>, <c>"s2twp"</c>, <c>"t2hk"</c>).
        /// Case-insensitive and culture-invariant.
        /// </param>
        /// <param name="configId">
        /// When this method returns <c>true</c>, contains the parsed <see cref="OpenccConfig"/> value;
        /// otherwise set to <c>default</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the name is recognized by the native library; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This is a thin wrapper over the native C API and exists for advanced interop/debugging scenarios.
        /// </para>
        /// <para>
        /// Prefer <see cref="OpenccConfigExtensions.Parse"/> / <see cref="OpenccConfigExtensions.IsValidConfig"/>
        /// for typical managed usage.
        /// </para>
        /// <para>
        /// This method does not set the OpenCC last-error state.
        /// </para>
        /// </remarks>
        public static bool ConfigNameToIdNative(string name, out OpenccConfig configId)
        {
            configId = default;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            // UTF-8 + '\0'
            var bytes = Utf8Z.FromString(name);

            if (!OpenccFmmsegNative.opencc_config_name_to_id(bytes, out var id))
                return false;

            configId = (OpenccConfig)id;
            return true;
        }

        /// <summary>
        /// Attempts to map a typed <see cref="OpenccConfig"/> value to its canonical lowercase name
        /// using the native C API (<c>opencc_config_id_to_name</c>).
        /// </summary>
        /// <param name="configId">The OpenCC configuration identifier.</param>
        /// <param name="name">
        /// When this method returns <c>true</c>, contains the canonical lowercase name
        /// (for example, <c>"s2t"</c> or <c>"t2hk"</c>); otherwise set to an empty string.
        /// </param>
        /// <returns>
        /// <c>true</c> if <paramref name="configId"/> is recognized by the native library; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This is a thin wrapper over the native C API and exists for advanced interop/debugging scenarios.
        /// </para>
        /// <para>
        /// Prefer <see cref="OpenccConfigExtensions.ToCanonicalName"/> for typical managed usage.
        /// </para>
        /// <para>
        /// This method does not set the OpenCC last-error state.
        /// </para>
        /// </remarks>
        public static bool ConfigIdToNameNative(OpenccConfig configId, out string name)
        {
            var ptr = OpenccFmmsegNative.opencc_config_id_to_name((int)configId);
            if (ptr == IntPtr.Zero)
            {
                name = string.Empty;
                return false;
            }

            name = Utf8Z.ToStringZ(ptr);
            return true;
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the instance has been disposed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpenccFmmseg), "The OpenCC instance has been disposed.");
        }
    }
}