using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenccFmmsegLib
{
    internal static class Utf8Z
    {
        /// <summary>
        /// Allocates a UTF-8 encoded, null-terminated (C-string) byte array
        /// from a managed <see cref="string"/>.
        /// </summary>
        /// <param name="str">
        /// The input string. If <c>null</c>, a single null byte (<c>0x00</c>) is returned.
        /// </param>
        /// <returns>
        /// A newly allocated UTF-8 byte array terminated with a null byte,
        /// suitable for passing to native C APIs.
        /// </returns>
        /// <remarks>
        /// This method always allocates a new managed array.
        /// For large or frequently used strings, prefer pooled helpers
        /// such as <see cref="Rent"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] FromString(string str)
        {
            if (str == null)
                return new byte[] { 0 };

            var byteCount = Encoding.UTF8.GetByteCount(str);
            var buffer = new byte[byteCount + 1];
            Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, 0);
            buffer[byteCount] = 0;
            return buffer;
        }

        /// <summary>
        /// Decodes a NUL-terminated UTF-8 string from unmanaged memory into a managed <see cref="string"/>.
        /// </summary>
        /// <param name="ptr">
        /// Pointer to a UTF-8 encoded, NUL-terminated byte sequence.
        /// </param>
        /// <returns>
        /// The decoded managed string. If <paramref name="ptr"/> is <see cref="IntPtr.Zero"/>,
        /// an empty string is returned.
        /// </returns>
        /// <remarks>
        /// This method scans memory byte-by-byte until a NUL terminator (0x00) is found,
        /// then decodes the preceding bytes using strict UTF-8 decoding.
        /// The caller must ensure that the pointer refers to valid, readable memory.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe string ToStringZ(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;

            var bytePtr = (byte*)ptr;
            var length = 0;

            for (var p = bytePtr; *p != 0; p++)
                length++;

            return Utf8Strict.GetString(bytePtr, length);
        }

        /// <summary>
        /// Shared UTF-8 decoder instance.
        /// Configured to never emit a BOM and to throw on invalid byte sequences.
        /// </summary>
        private static readonly UTF8Encoding Utf8Strict =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>
        /// Rents a UTF-8 encoded, NUL-terminated buffer from <see cref="ArrayPool{T}"/>.
        /// </summary>
        /// <param name="s">
        /// The input string to encode. If <see langword="null"/>, it is treated as an empty string.
        /// </param>
        /// <param name="rented">
        /// Receives the rented byte buffer containing the UTF-8 encoded data
        /// followed by a trailing NUL byte (<c>0x00</c>).
        /// </param>
        /// <param name="byteCount">
        /// Receives the number of UTF-8 bytes written excluding the trailing NUL byte.
        /// The total valid buffer length is <c>byteCount + 1</c>.
        /// </param>
        /// <remarks>
        /// <para>
        /// The returned buffer is suitable for passing to native C APIs that expect
        /// a NUL-terminated UTF-8 string (<c>char*</c>).
        /// </para>
        /// <para>
        /// The caller <b>must</b> return the buffer to the shared pool by calling
        /// <see cref="Return(byte[])"/> (typically in a <c>finally</c> block).
        /// </para>
        /// <para>
        /// The buffer may be larger than required due to pooling. Only the first
        /// <c>byteCount + 1</c> bytes are guaranteed to contain valid data.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Rent(string s, out byte[] rented, out int byteCount)
        {
            if (s == null) s = string.Empty;

            byteCount = Encoding.UTF8.GetByteCount(s);
            rented = ArrayPool<byte>.Shared.Rent(byteCount + 1);
            Encoding.UTF8.GetBytes(s, 0, s.Length, rented, 0);
            rented[byteCount] = 0;
        }

        /// <summary>
        /// Returns a previously rented UTF-8 buffer to the shared <see cref="ArrayPool{T}"/>.
        /// </summary>
        /// <param name="rented">
        /// The buffer obtained from <see cref="Rent"/>.
        /// If <see langword="null"/>, the call is ignored.
        /// </param>
        /// <remarks>
        /// <para>
        /// After returning the buffer, it must no longer be accessed by the caller.
        /// </para>
        /// <para>
        /// The contents of pooled buffers are not cleared by default.
        /// Do not use pooled buffers for sensitive data unless explicitly cleared.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(byte[] rented)
        {
            if (rented != null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }
}