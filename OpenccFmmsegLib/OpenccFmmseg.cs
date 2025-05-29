using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Buffers;
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

        /// <summary>
        /// Supported configuration names for conversion.
        /// </summary>
        private static readonly HashSet<string> ConfigList = new HashSet<string>(StringComparer.Ordinal)
        {
            "s2t", "t2s", "s2tw", "tw2s", "s2twp", "tw2sp", "s2hk", "hk2s", "t2tw", "t2twp", "t2hk", "tw2t", "tw2tp",
            "hk2t", "t2jp", "jp2t"
        };

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

            var inputByteCount = Encoding.UTF8.GetByteCount(input);
            var configByteCount = Encoding.UTF8.GetByteCount(config);

            byte[] inputBytes = null;
            byte[] configBytes = null;
            var pool = ByteArrayPool.Value;
            var inputFromPool = false;
            var configFromPool = false;

            try
            {
                // For input bytes - use direct allocation for small strings (simpler and just as efficient)
                if (inputByteCount <= 1024)
                {
                    // Allocate new array and encode. It will be exactly inputByteCount long.
                    // We need to create a new array with +1 size and copy to ensure null-termination.
                    byte[] tempBytes = Encoding.UTF8.GetBytes(input);
                    inputBytes = new byte[tempBytes.Length + 1];
                    Buffer.BlockCopy(tempBytes, 0, inputBytes, 0, tempBytes.Length);
                    inputBytes[tempBytes.Length] = 0x00; // Null-terminate
                }
                else
                {
                    // Rent from pool, ensure +1 size for null terminator
                    inputBytes = pool.Rent(inputByteCount + 1);
                    inputFromPool = true;
                    // Encoding.UTF8.GetBytes returns the number of bytes written
                    var actualBytesWritten = Encoding.UTF8.GetBytes(input, 0, input.Length, inputBytes, 0);
                    inputBytes[actualBytesWritten] = 0x00; // Null-terminate at the actual end of written data
                }

                // For config bytes - direct allocation (simpler)
                if (configByteCount <= 128)
                {
                    // Allocate new array and encode. Similar to inputBytes small case.
                    byte[] tempBytes = Encoding.UTF8.GetBytes(config);
                    configBytes = new byte[tempBytes.Length + 1];
                    Buffer.BlockCopy(tempBytes, 0, configBytes, 0, tempBytes.Length);
                    configBytes[tempBytes.Length] = 0x00; // Null-terminate
                }
                else
                {
                    // Rent from pool, ensure +1 size for null terminator
                    configBytes = pool.Rent(configByteCount + 1);
                    configFromPool = true;
                    var actualBytesWritten = Encoding.UTF8.GetBytes(config, 0, config.Length, configBytes, 0);
                    configBytes[actualBytesWritten] = 0x00; // Null-terminate at the actual end of written data
                }

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
                // Return rented arrays to pool
                if (inputFromPool && inputBytes != null)
                    pool.Return(inputBytes);
                if (configFromPool && configBytes != null)
                    pool.Return(configBytes);
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
            if (string.IsNullOrEmpty(input)) return 0;

            byte[] inputBytes = null;
            var pool = ByteArrayPool.Value;
            var inputFromPool = false;

            try
            {
                var inputByteCount = Encoding.UTF8.GetByteCount(input);

                if (inputByteCount <= 1024) // Reusing the same threshold for consistency
                {
                    byte[] tempBytes = Encoding.UTF8.GetBytes(input);
                    inputBytes = new byte[tempBytes.Length + 1];
                    Buffer.BlockCopy(tempBytes, 0, inputBytes, 0, tempBytes.Length);
                    inputBytes[tempBytes.Length] = 0x00; // Null-terminate
                }
                else
                {
                    inputBytes = pool.Rent(inputByteCount + 1);
                    inputFromPool = true;
                    var actualBytesWritten = Encoding.UTF8.GetBytes(input, 0, input.Length, inputBytes, 0);
                    inputBytes[actualBytesWritten] = 0x00; // Null-terminate
                }

                return opencc_zho_check(_openccInstance, inputBytes);
            }
            finally
            {
                if (inputFromPool && inputBytes != null)
                    pool.Return(inputBytes);
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
        /// <returns>The managed string, or null if the pointer is zero.</returns>
        private static unsafe string Utf8BytesToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            var bytePtr = (byte*)ptr;
            var length = 0;

            // Find null-terminator length            
            for (byte* p = bytePtr; *p != 0; p++)
            {
                length++;
            }

            // Decode directly from the unmanaged memory
            return Encoding.UTF8.GetString(bytePtr, length);
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the instance has been disposed.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpenccFmmseg), "The OpenCC instance has been disposed.");
        }
    }
}