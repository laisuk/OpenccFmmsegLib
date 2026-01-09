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
            new Dictionary<string, byte[]>(StringComparer.Ordinal);

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

                var byteCount = Encoding.UTF8.GetByteCount(canonical);
                var encodedBytes = new byte[byteCount + 1];
                Encoding.UTF8.GetBytes(canonical, 0, canonical.Length, encodedBytes, 0);
                encodedBytes[byteCount] = 0x00; // NUL

                EncodedConfigCache[canonical] = encodedBytes;
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
        /// Converts the input Chinese text using the specified OpenCC configuration.
        /// </summary>
        /// <param name="input">The input string to convert.</param>
        /// <param name="config">The conversion configuration (e.g., "s2t", "t2s").</param>
        /// <param name="punctuation">Whether to convert punctuation as well.</param>
        /// <returns>The converted string, or an empty string if input is null or empty.</returns>
        public string Convert(string input, string config, bool punctuation = false)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            ThrowIfDisposed();

            // 🔹 SINGLE OWNER: parse config via managed extensions
            if (!OpenccConfigExtensions.TryParseConfig(config, out var configId))
            {
                configId = OpenccConfigExtensions.DefaultConfig();
            }

            // 🔹 canonicalize once
            var canonical = configId.ToCanonicalName();

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
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            ThrowIfDisposed();

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
            var inputByteCount = Encoding.UTF8.GetByteCount(input);
            byte[] inputBuffer = null;

            try
            {
                inputBuffer = ArrayPool<byte>.Shared.Rent(inputByteCount + 1);

                var inputBytesWritten = Encoding.UTF8.GetBytes(input, 0, input.Length, inputBuffer, 0);
                inputBuffer[inputBytesWritten] = 0x00;

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
                    return Utf8BytesToString(output);
                }
                finally
                {
                    if (output != IntPtr.Zero)
                        OpenccFmmsegNative.opencc_string_free(output);
                }
            }
            finally
            {
                if (inputBuffer != null)
                    ArrayPool<byte>.Shared.Return(inputBuffer);
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

            var inputByteCount = Encoding.UTF8.GetByteCount(input);
            byte[] inputBuffer = null;

            try
            {
                inputBuffer = ArrayPool<byte>.Shared.Rent(inputByteCount + 1);

                var written = Encoding.UTF8.GetBytes(input, 0, input.Length, inputBuffer, 0);
                inputBuffer[written] = 0x00; // NUL

                var output =
                    OpenccFmmsegNative.opencc_convert_cfg(_openccInstance, inputBuffer, configNum, punctuation);

                try
                {
                    return Utf8BytesToString(output);
                }
                finally
                {
                    if (output != IntPtr.Zero)
                        OpenccFmmsegNative.opencc_string_free(output);
                }
            }
            finally
            {
                if (inputBuffer != null)
                    ArrayPool<byte>.Shared.Return(inputBuffer);
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

            // Compute needed size first (pure, non-allocating & won’t rent yet)
            var inputByteCount = Encoding.UTF8.GetByteCount(input);

            byte[] buffer = null;
            try
            {
                buffer = ArrayPool<byte>.Shared.Rent(inputByteCount + 1);

                var bytesWritten = Encoding.UTF8.GetBytes(input, 0, input.Length, buffer, 0);
                buffer[bytesWritten] = 0; // null-terminate

                return OpenccFmmsegNative.opencc_zho_check(_openccInstance, buffer);
            }
            finally
            {
                if (buffer != null)
                    ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Gets the last error message from the native OpenCC library.
        /// </summary>
        /// <returns>The last error message, or an empty string if none.</returns>
        public static string LastError()
        {
            var cLastError = OpenccFmmsegNative.opencc_last_error();
            if (cLastError == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Utf8BytesToString(cLastError);
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
        /// Prefer <see cref="OpenccConfigExtensions.FromStr"/> / <see cref="OpenccConfigExtensions.IsSupportedConfig"/>
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
            var bytes = Encoding.UTF8.GetBytes(name + "\0");

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

            name = Utf8BytesToString(ptr);
            return true;
        }

        /// <summary>
        /// Shared UTF-8 decoder instance.
        /// Configured to never emit a BOM and to throw on invalid byte sequences.
        /// </summary>
        private static readonly UTF8Encoding Utf8Strict =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>
        /// Converts a null-terminated UTF-8 string from unmanaged memory into a managed string.
        /// </summary>
        /// <param name="ptr">
        /// Pointer to the start of a NUL-terminated UTF-8 byte sequence in unmanaged memory.
        /// </param>
        /// <returns>
        /// The decoded managed string, or <c>null</c> if <paramref name="ptr"/> is <see cref="IntPtr.Zero"/>.
        /// </returns>
        /// <remarks>
        /// This method scans memory one byte at a time until it encounters a NUL terminator (0x00),
        /// then decodes the collected bytes as UTF-8. It is safe because it never reads beyond
        /// the terminator. The caller must ensure that the pointer refers to valid, accessible memory.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe string Utf8BytesToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;

            var bytePtr = (byte*)ptr;
            var length = 0;

            // Find null-terminator length
            for (var p = bytePtr; *p != 0; p++)
            {
                length++;
            }

            return Utf8Strict.GetString(bytePtr, length);
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