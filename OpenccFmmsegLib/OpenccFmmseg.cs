using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

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
        // Define constants
        private const string DllPath = "opencc_fmmseg_capi";

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

        // Thread-local buffer pool for byte arrays to reduce allocations
        private static readonly ThreadLocal<ArrayPool<byte>> ByteArrayPool =
            new ThreadLocal<ArrayPool<byte>>(() => ArrayPool<byte>.Shared);

        // Define DLL functions using P/Invoke
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr opencc_new();

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void opencc_delete(IntPtr opencc);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr opencc_convert(IntPtr opencc, byte[] input, byte[] config, bool punctuation);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int opencc_zho_check(IntPtr opencc, byte[] input);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void opencc_string_free(IntPtr str);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr opencc_last_error();

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void opencc_error_free(IntPtr str);

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
            _openccInstance = opencc_new();
            if (_openccInstance != IntPtr.Zero) return;
            var lastError = GetLastErrorInternal();
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
                opencc_delete(_openccInstance);
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

            return ConvertInternal(input, config, punctuation);
        }

        /// <summary>
        /// Internal conversion logic. Handles encoding, buffer pooling, and native call.
        /// </summary>
        private string ConvertInternal(string input, string config, bool punctuation)
        {
            ThrowIfDisposed();

            var pool = ByteArrayPool.Value;
            byte[] inputBytes = null;
            var inputRented = false;

            try
            {
                // Prepare input buffer
                var inputByteCount = Encoding.UTF8.GetByteCount(input);
                var inputBufferSize = inputByteCount + 1;

                inputBytes = pool.Rent(inputBufferSize);
                inputRented = true;

                var inputBytesWritten = Encoding.UTF8.GetBytes(input, 0, input.Length, inputBytes, 0);
                inputBytes[inputBytesWritten] = 0x00; // Null-terminate

                // Prepare config buffer
                if (!EncodedConfigCache.TryGetValue(config, out var configBytes))
                {
                    throw new ArgumentException($"Unsupported OpenCC config: {config}", nameof(config));
                }

                // Native call
                var output = opencc_convert(_openccInstance, inputBytes, configBytes, punctuation);

                try
                {
                    return Utf8BytesToString(output);
                }
                finally
                {
                    if (output != IntPtr.Zero)
                        opencc_string_free(output);
                }
            }
            finally
            {
                if (inputRented && inputBytes != null)
                    pool.Return(inputBytes);
            }
        }

        /// <summary>
        /// Checks if the input string is Chinese text using the OpenCC language check.
        /// </summary>
        /// <param name="input">The input string to check.</param>
        /// <returns>An integer indicating the result of the check (implementation-defined by OpenCC).</returns>
        public int ZhoCheck(string input)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(input))
                return 0;

            var pool = ByteArrayPool.Value;
            byte[] buffer = null;
            var rented = false;

            try
            {
                var byteCount = Encoding.UTF8.GetByteCount(input);
                var bufferSize = byteCount + 1; // for null terminator

                // Always rent from the pool to avoid heap allocation
                buffer = pool.Rent(bufferSize);
                rented = true;

                int bytesWritten = Encoding.UTF8.GetBytes(input, 0, input.Length, buffer, 0);
                buffer[bytesWritten] = 0x00; // Null-terminate

                return opencc_zho_check(_openccInstance, buffer);
            }
            finally
            {
                if (rented && buffer != null)
                {
                    pool.Return(buffer);
                }
            }
        }

        /// <summary>
        /// Gets the last error message from the native OpenCC library.
        /// </summary>
        /// <returns>The last error message, or an empty string if none.</returns>
        public static string LastError()
        {
            return GetLastErrorInternal();
        }

        /// <summary>
        /// Internal helper to retrieve the last error from the native library.
        /// </summary>
        private static string GetLastErrorInternal()
        {
            var cLastError = opencc_last_error();
            if (cLastError == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Utf8BytesToString(cLastError);
            }
            finally
            {
                opencc_error_free(cLastError);
            }
        }

        /// <summary>
        /// Converts a UTF-8 null-terminated string from unmanaged memory to a managed string.
        /// </summary>
        /// <param name="ptr">Pointer to the unmanaged UTF-8 string.</param>
        /// <param name="maxLength"></param>
        /// <returns>The managed string, or null if the pointer is zero.</returns>
        // private static unsafe string Utf8BytesToString(IntPtr ptr)
        // {
        //     if (ptr == IntPtr.Zero)
        //         return null;
        //
        //     var bytePtr = (byte*)ptr;
        //     var length = 0;
        //
        //     // Find null-terminator length            
        //     for (byte* p = bytePtr; *p != 0; p++)
        //     {
        //         length++;
        //     }
        //
        //     // Decode directly from the unmanaged memory
        //     return Encoding.UTF8.GetString(bytePtr, length);
        // }
        private static unsafe string Utf8BytesToString(IntPtr ptr, int maxLength = 0)
        {
            if (ptr == IntPtr.Zero)
                return null;

            var bytePtr = (byte*)ptr;
            var index = 0;

            var limit = maxLength > 0 ? maxLength : int.MaxValue;

            // Safe byte-by-byte fallback scan
            bool SafeByteScan(int startIndex, int endIndex)
            {
                for (var i = startIndex; i < endIndex; i++)
                {
                    if (bytePtr[i] != 0) continue;
                    index = i;
                    return true;
                }

                index = endIndex;
                return false;
            }

            try
            {
                if (IntPtr.Size == 8)
                {
                    // 64-bit batch scan
                    while (index + 8 <= limit)
                    {
                        var chunk = *((ulong*)(bytePtr + index));
                        if (HasZeroByte64(chunk))
                        {
                            for (int offset = 0; offset < 8; offset++)
                            {
                                if (bytePtr[index + offset] != 0) continue;
                                index += offset;
                                return Encoding.UTF8.GetString(bytePtr, index);
                            }
                        }

                        index += 8;
                    }
                }
                else
                {
                    // 32-bit batch scan
                    while (index + 4 <= limit)
                    {
                        var chunk = *((uint*)(bytePtr + index));
                        if (HasZeroByte32(chunk))
                        {
                            for (var offset = 0; offset < 4; offset++)
                            {
                                if (bytePtr[index + offset] != 0) continue;
                                index += offset;
                                return Encoding.UTF8.GetString(bytePtr, index);
                            }
                        }

                        index += 4;
                    }
                }
            }
            catch
            {
                return SafeByteScan(index, limit) ? Encoding.UTF8.GetString(bytePtr, index) : null;
            }

            // Finish scanning byte-by-byte within limit
            return SafeByteScan(index, limit) ? Encoding.UTF8.GetString(bytePtr, index) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasZeroByte64(ulong x)
        {
            return ((x - 0x0101010101010101UL) & ~x & 0x8080808080808080UL) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasZeroByte32(uint x)
        {
            return ((x - 0x01010101U) & ~x & 0x80808080U) != 0;
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