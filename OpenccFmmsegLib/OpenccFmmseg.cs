using System;
using System.Collections.Generic;
using System.Text;
using System.Buffers;
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

        // Supported configuration names for OpenCC conversion
        private static readonly HashSet<string> ConfigList = new HashSet<string>(
            new[]
            {
                "s2t", "t2s", "s2tw", "tw2s", "s2twp", "tw2sp", "s2hk", "hk2s",
                "t2tw", "t2twp", "t2hk", "tw2t", "tw2tp", "hk2t", "t2jp", "jp2t"
            },
            StringComparer.Ordinal);

        private IntPtr _openccInstance;
        private bool _disposed;

        // Static constructor to pre-encode common config strings for efficient native interop
        static OpenccFmmseg()
        {
            foreach (var config in ConfigList)
            {
                if (EncodedConfigCache.ContainsKey(config))
                    continue; // Defensive: avoid ArgumentException if code is refactored later

                var byteCount = Encoding.UTF8.GetByteCount(config);
                var encodedBytes = new byte[byteCount + 1];
                Encoding.UTF8.GetBytes(config, 0, config.Length, encodedBytes, 0);
                encodedBytes[byteCount] = 0x00;

                EncodedConfigCache[config] = encodedBytes;
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
            // Early return for null/empty input
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Use ordinal comparison for better performance
            if (!ConfigList.Contains(config))
                config = "s2t";

            ThrowIfDisposed();

            // Compute needed size first (pure, non-allocating & won’t rent yet)
            var inputByteCount = Encoding.UTF8.GetByteCount(input);
            byte[] inputBuffer = null;

            try
            {
                inputBuffer = ArrayPool<byte>.Shared.Rent(inputByteCount + 1);

                var inputBytesWritten = Encoding.UTF8.GetBytes(input, 0, input.Length, inputBuffer, 0);
                inputBuffer[inputBytesWritten] = 0x00; // Null-terminate

                // Prepare config buffer
                if (!EncodedConfigCache.TryGetValue(config, out var configBytes))
                {
                    throw new ArgumentException($"Unsupported OpenCC config: {config}", nameof(config));
                }

                // Native call
                var output = OpenccFmmsegNative.opencc_convert(_openccInstance, inputBuffer, configBytes, punctuation);

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
                {
                    ArrayPool<byte>.Shared.Return(inputBuffer);
                }
            }
        }

        /// <summary>
        /// Converts the input Chinese text using a numeric OpenCC config (opencc_config_t).
        /// </summary>
        /// <param name="input">The input string to convert.</param>
        /// <param name="config">Numeric config value (e.g. OPENCC_CONFIG_S2TWP).</param>
        /// <param name="punctuation">Whether to convert punctuation as well.</param>
        /// <returns>
        /// The converted string. If <paramref name="config"/> is invalid, the native side returns
        /// an allocated error message string like "Invalid config: &lt;value&gt;" (and also stores it as last error).
        /// Returns empty string only if input is null/empty, or if native returned NULL.
        /// </returns>
        private string ConvertCfgCore(string input, int config, bool punctuation = false)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            ThrowIfDisposed();

            var inputByteCount = Encoding.UTF8.GetByteCount(input);
            byte[] inputBuffer = null;

            try
            {
                inputBuffer = ArrayPool<byte>.Shared.Rent(inputByteCount + 1);

                var written = Encoding.UTF8.GetBytes(input, 0, input.Length, inputBuffer, 0);
                inputBuffer[written] = 0x00; // NUL

                var output = OpenccFmmsegNative.opencc_convert_cfg(_openccInstance, inputBuffer, config, punctuation);

                try
                {
                    // NOTE: native contract says invalid config may still return an allocated error message string.
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
        /// Converts the input Chinese text using a numeric OpenCC config (typed enum).
        /// </summary>
        public string ConvertCfg(string input, OpenccConfig config, bool punctuation = false)
        {
            return ConvertCfgCore(input, (int)config, punctuation);
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

        internal static bool TryParseConfig(
            string name,
            out OpenccConfig config)
        {
            config = default;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            // UTF-8 + '\0'
            var bytes = Encoding.UTF8.GetBytes(name + "\0");

            if (!OpenccFmmsegNative.opencc_config_name_to_id(bytes, out var id))
                return false;

            config = (OpenccConfig)id;
            return true;
        }

        internal static bool TryGetConfigName(OpenccConfig config, out string name)
        {
            var ptr = OpenccFmmsegNative.opencc_config_id_to_name((int)config);
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

    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo

    /// <summary>
    /// Numeric OpenCC configuration identifiers (opencc_config_t).
    /// These values must match the native enum exactly.
    /// </summary>
    // NOTE:
    // Enum member names intentionally follow OpenCC canonical identifiers
    // (S2TW, T2HK, etc.) to preserve cross-language consistency.
    // Do NOT rename to satisfy C# naming rules.
    public enum OpenccConfig
    {
        /// <summary>Simplified Chinese → Traditional Chinese</summary>
        S2T = 1,

        /// <summary>Simplified → Traditional (Taiwan)</summary>
        S2TW = 2,

        /// <summary>Simplified → Traditional (Taiwan, with phrases)</summary>
        S2TWP = 3,

        /// <summary>Simplified → Traditional (Hong Kong)</summary>
        S2HK = 4,

        /// <summary>Traditional Chinese → Simplified Chinese</summary>
        T2S = 5,

        /// <summary>Traditional → Taiwan Traditional</summary>
        T2TW = 6,

        /// <summary>Traditional → Taiwan Traditional (with phrases)</summary>
        T2TWP = 7,

        /// <summary>Traditional → Hong Kong Traditional</summary>
        T2HK = 8,

        /// <summary>Taiwan Traditional → Simplified</summary>
        TW2S = 9,

        /// <summary>Taiwan Traditional → Simplified (variant)</summary>
        TW2SP = 10,

        /// <summary>Taiwan Traditional → Traditional</summary>
        TW2T = 11,

        /// <summary>Taiwan Traditional → Traditional (variant)</summary>
        TW2TP = 12,

        /// <summary>Hong Kong Traditional → Simplified</summary>
        HK2S = 13,

        /// <summary>Hong Kong Traditional → Traditional</summary>
        HK2T = 14,

        /// <summary>Japanese Kanji variants → Traditional Chinese</summary>
        JP2T = 15,

        /// <summary>Traditional Chinese → Japanese Kanji variants</summary>
        T2JP = 16
    }

    // ReSharper restore IdentifierTypo
    // ReSharper restore InconsistentNaming

    /// <summary>
    /// Extension helpers for <see cref="OpenccConfig"/>.
    /// </summary>
    /// <remarks>
    /// This class provides utility methods that operate on <see cref="OpenccConfig"/>
    /// without exposing numeric configuration IDs or native OpenCC details.
    /// </remarks>
    public static class OpenccConfigExtensions
    {
        /// <summary>
        /// Converts an <see cref="OpenccConfig"/> value to its canonical OpenCC
        /// configuration name.
        /// </summary>
        /// <param name="config">
        /// The OpenCC configuration enum value.
        /// </param>
        /// <returns>
        /// A lowercase canonical configuration name
        /// (for example, <c>"s2t"</c>, <c>"s2twp"</c>, <c>"t2hk"</c>),
        /// suitable for logging, display, file naming, or interoperability
        /// with OpenCC-compatible tools.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The returned string follows the official OpenCC canonical naming
        /// convention and is independent of the enum member name casing.
        /// </para>
        /// <para>
        /// This method does not perform any allocation beyond returning the
        /// constant string literal and does not invoke native code.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="config"/> is not a valid <see cref="OpenccConfig"/> value.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToCanonicalName(this OpenccConfig config)
        {
            switch (config)
            {
                case OpenccConfig.S2T: return "s2t";
                case OpenccConfig.S2TW: return "s2tw";
                case OpenccConfig.S2TWP: return "s2twp";
                case OpenccConfig.S2HK: return "s2hk";
                case OpenccConfig.T2S: return "t2s";
                case OpenccConfig.T2TW: return "t2tw";
                case OpenccConfig.T2TWP: return "t2twp";
                case OpenccConfig.T2HK: return "t2hk";
                case OpenccConfig.TW2S: return "tw2s";
                case OpenccConfig.TW2SP: return "tw2sp";
                case OpenccConfig.TW2T: return "tw2t";
                case OpenccConfig.TW2TP: return "tw2tp";
                case OpenccConfig.HK2S: return "hk2s";
                case OpenccConfig.HK2T: return "hk2t";
                case OpenccConfig.JP2T: return "jp2t";
                case OpenccConfig.T2JP: return "t2jp";
                default:
                    throw new ArgumentOutOfRangeException(nameof(config), config, "Invalid OpenCC config");
            }
        }
    }
}