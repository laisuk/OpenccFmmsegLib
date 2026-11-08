using System;
using System.Runtime.InteropServices;

namespace OpenccFmmsegLib
{
    /// <summary>
    /// Provides the raw P/Invoke declarations for the native <c>opencc_fmmseg_capi</c> library.
    /// </summary>
    /// <remarks>
    /// This class is internal and should not be used directly by consumers.
    /// It defines all unmanaged entry points required by <see cref="OpenccFmmseg"/>,
    /// mapping the native C API functions one-to-one via <see cref="DllImportAttribute"/>.
    /// <para>
    /// The managed wrapper <see cref="OpenccFmmseg"/> handles resource lifetime,
    /// error checking, and UTF-8 marshaling. Callers must never invoke these
    /// functions directly unless they understand the unmanaged contract.
    /// </para>
    /// </remarks>
    internal static class OpenccFmmsegNative
    {
        /// <summary>
        /// The name of the native shared library (<c>opencc_fmmseg_capi</c>).
        /// The .NET runtime automatically appends the correct extension based on the platform:
        /// <list type="bullet">
        /// <item><description><c>.dll</c> on Windows</description></item>
        /// <item><description><c>.so</c> on Linux</description></item>
        /// <item><description><c>.dylib</c> on macOS</description></item>
        /// </list>
        /// </summary>
        private const string DllPath = "opencc_fmmseg_capi";

        /// <summary>
        /// Gets the native C API binary interface version.
        /// </summary>
        /// <remarks>
        /// The ABI number identifies breaking interface changes; additive exports can require a newer library without changing it.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint opencc_abi_number();

        /// <summary>
        /// Gets the native library version identifier.
        /// </summary>
        /// <remarks>
        /// Returns a static NUL-terminated UTF-8 pointer, valid while the library is loaded. Do not modify or free it.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr opencc_version_string();

        /// <summary>
        /// Creates a converter using the built-in dictionaries.
        /// </summary>
        /// <remarks>
        /// Returns an owned instance, or zero on failure. Release it with opencc_delete; read opencc_last_error immediately on failure.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_new();

        // Layout follows opencc_fmmseg_capi.h; size_t is pointer-sized on every RID.
        [StructLayout(LayoutKind.Sequential)]
        internal struct CustomPair
        {
            internal IntPtr Source;
            internal IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CustomDictionarySpecification
        {
            internal uint Slot;
            internal uint Mode;
            internal IntPtr Pairs;
            internal UIntPtr PairCount;
        }

        /// <summary>
        /// Creates a converter with ordered custom dictionary specifications.
        /// </summary>
        /// <param name="specs">Borrowed contiguous native specification array; may be zero when specCount is zero.</param>
        /// <param name="specCount">Specification count, represented by native size_t.</param>
        /// <remarks>
        /// Requires native v0.11.5 or later; Seal slots require v0.13.0. Copies all borrowed specifications and mappings before returning. Returns an owned instance or zero on failure; release it with opencc_delete.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr opencc_new_custom(IntPtr specs, UIntPtr specCount);

        /// <summary>
        /// Normalizes mapped CJK Compatibility Ideographs.
        /// </summary>
        /// <param name="instance">Pointer to a live native instance, unchanged and not freed during the call.</param>
        /// <param name="input">Borrowed NUL-terminated UTF-8 input, unchanged during the call.</param>
        /// <returns>Newly allocated NUL-terminated UTF-8 output; native failures may return allocated error text or a zero pointer.</returns>
        /// <remarks>
        /// Requires native v0.12.0 or later. Leaves characters without a CJK compatibility mapping unchanged.
        /// Release every nonzero result, including error text, exactly once with <see cref="opencc_string_free"/>.
        /// Read <see cref="opencc_last_error"/> immediately on the calling thread to diagnose failure.
        /// Null instance/input returns zero; invalid UTF-8 returns allocated error text. Success clears the thread-local diagnostic.
        /// Performs no Simplified/Traditional conversion or general-purpose Unicode normalization.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr opencc_normalize_compat(IntPtr instance, byte[] input);

        /// <summary>
        /// Applies CJK Compatibility Ideograph normalization followed by curated Unicode mappings.
        /// </summary>
        /// <param name="instance">Pointer to a live native instance, unchanged and not freed during the call.</param>
        /// <param name="input">Borrowed NUL-terminated UTF-8 input, unchanged during the call.</param>
        /// <returns>Newly allocated NUL-terminated UTF-8 output; native failures may return allocated error text or a zero pointer.</returns>
        /// <remarks>
        /// Requires native v0.12.0 or later. Combines the CJK and curated Unicode tables.
        /// Release every nonzero result, including error text, exactly once with <see cref="opencc_string_free"/>.
        /// Read <see cref="opencc_last_error"/> immediately on the calling thread to diagnose failure.
        /// Null instance/input returns zero; invalid UTF-8 returns allocated error text. Success clears the thread-local diagnostic.
        /// Performs no Simplified/Traditional conversion or general-purpose Unicode normalization.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr opencc_normalize_compat_extended(IntPtr instance, byte[] input);

        /// <summary>
        /// Normalizes selected radicals, glyph variants, punctuation, and text-extraction artifacts.
        /// </summary>
        /// <param name="instance">Pointer to a live native instance, unchanged and not freed during the call.</param>
        /// <param name="input">Borrowed NUL-terminated UTF-8 input, unchanged during the call.</param>
        /// <returns>Newly allocated NUL-terminated UTF-8 output; native failures may return allocated error text or a zero pointer.</returns>
        /// <remarks>
        /// Requires native v0.13.1.0 or later. Uses only the curated Unicode table; CJK Compatibility Ideographs are preserved unless explicitly mapped by that table.
        /// Release every nonzero result, including error text, exactly once with <see cref="opencc_string_free"/>.
        /// Read <see cref="opencc_last_error"/> immediately on the calling thread to diagnose failure.
        /// Null instance/input returns zero; invalid UTF-8 returns allocated error text. Success clears the thread-local diagnostic.
        /// Performs no Simplified/Traditional conversion or general-purpose Unicode normalization.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr opencc_normalize_unicode_compat(IntPtr instance, byte[] input);

        /// <summary>
        /// Applies built-in display fallback at an inclusive CJK extension threshold.
        /// </summary>
        /// <param name="instance">Pointer to a live native instance, unchanged and not freed during the call.</param>
        /// <param name="input">Borrowed NUL-terminated UTF-8 input, unchanged during the call.</param>
        /// <param name="level">Native uint32_t threshold, from 0 (Extension B) through 7 (Extension I), inclusive of supported later extensions.</param>
        /// <returns>Newly allocated NUL-terminated UTF-8 output; native failures may return allocated error text or a zero pointer.</returns>
        /// <remarks>
        /// Requires native v0.12.0 or later. Characters without an eligible fallback mapping remain unchanged.
        /// Release every nonzero result, including error text, exactly once with <see cref="opencc_string_free"/>.
        /// Read <see cref="opencc_last_error"/> immediately on the calling thread to diagnose failure.
        /// Null instance/input returns zero; invalid UTF-8 returns allocated error text. Success clears the thread-local diagnostic.
        /// Performs no Simplified/Traditional conversion or general-purpose Unicode normalization.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr opencc_detofu(IntPtr instance, byte[] input, uint level);

        /// <summary>
        /// Releases a native converter and its resources.
        /// </summary>
        /// <param name="opencc">Owned instance returned by a native constructor, or zero.</param>
        /// <remarks>
        /// Zero is a no-op. Release each owned instance once; do not free it during another operation or use it afterward.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opencc_delete(IntPtr opencc);

        /// <summary>
        /// Converts NUL-terminated UTF-8 text using a configuration name.
        /// </summary>
        /// <param name="opencc">Pointer to a live native instance, unchanged and not freed during the call.</param>
        /// <param name="input">Borrowed NUL-terminated UTF-8 input, unchanged during the call.</param>
        /// <param name="config">Borrowed NUL-terminated UTF-8 configuration name; ASCII case-insensitive.</param>
        /// <param name="punctuation">Whether to apply the selected configuration's punctuation mappings.</param>
        /// <returns>Newly allocated NUL-terminated UTF-8 output; native failures may return allocated error text or a zero pointer.</returns>
        /// <remarks>
        /// Embedded NUL bytes terminate input.
        /// Release every nonzero result, including error text, exactly once with <see cref="opencc_string_free"/>.
        /// Read <see cref="opencc_last_error"/> immediately on the calling thread to diagnose failure.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_convert(
            IntPtr opencc,
            byte[] input,
            byte[] config,
            [MarshalAs(UnmanagedType.I1)] bool punctuation);

        /// <summary>
        /// Detects Chinese text type by comparing conversion results.
        /// </summary>
        /// <param name="opencc">Pointer to a live native instance, unchanged and not freed during the call.</param>
        /// <param name="input">Borrowed NUL-terminated UTF-8 input, unchanged during the call.</param>
        /// <remarks>
        /// Returns 1 for Traditional Chinese, 2 for Simplified Chinese, 0 for neither, or -1 for invalid input or a null pointer.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opencc_zho_check(IntPtr opencc, byte[] input);

        /// <summary>
        /// Releases a string allocated by a native conversion, normalization, or display-fallback operation.
        /// </summary>
        /// <param name="str">Original allocated pointer to release, or zero for a no-op.</param>
        /// <remarks>
        /// Zero is a no-op. Free each allocation once, preserving its first terminating NUL until this call. Do not pass static strings, caller-owned buffers, or opencc_last_error results.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opencc_string_free(IntPtr str);

        /// <summary>
        /// Copies the calling thread's last native error message.
        /// </summary>
        /// <remarks>
        /// Read immediately after failure on the same thread. Returns an independently allocated NUL-terminated UTF-8 string, containing "No error" when no error is recorded. Release every copy with opencc_error_free.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_last_error();

        /// <summary>
        /// Releases an error-message copy returned by opencc_last_error.
        /// </summary>
        /// <param name="str">Original allocated pointer to release, or zero for a no-op.</param>
        /// <remarks>
        /// Zero is a no-op. Free each nonzero copy once. Use opencc_string_free for transformation results.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opencc_error_free(IntPtr str);

        /// <summary>
        /// Converts NUL-terminated UTF-8 text using a numeric configuration ID.
        /// </summary>
        /// <param name="opencc">Pointer to a live native instance, unchanged and not freed during the call.</param>
        /// <param name="input">Borrowed NUL-terminated UTF-8 input, unchanged during the call.</param>
        /// <param name="config">Native opencc_config_t configuration ID.</param>
        /// <param name="punctuation">Whether to apply the selected configuration's punctuation mappings.</param>
        /// <returns>Newly allocated NUL-terminated UTF-8 output; native failures may return allocated error text or a zero pointer.</returns>
        /// <remarks>
        /// Embedded NUL bytes terminate input.
        /// Release every nonzero result, including error text, exactly once with <see cref="opencc_string_free"/>.
        /// Read <see cref="opencc_last_error"/> immediately on the calling thread to diagnose failure.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_convert_cfg(
            IntPtr opencc,
            byte[] input,
            int config,
            [MarshalAs(UnmanagedType.I1)] bool punctuation);

        /// <summary>
        /// Converts NUL-terminated UTF-8 input into a caller-owned buffer.
        /// </summary>
        /// <param name="opencc">Pointer to a live native instance, unchanged and not freed during the call.</param>
        /// <param name="input">Borrowed NUL-terminated UTF-8 input, unchanged during the call.</param>
        /// <param name="config">Native opencc_config_t configuration ID.</param>
        /// <param name="punctuation">Whether to apply the selected configuration's punctuation mappings.</param>
        /// <param name="outBuf">Caller-owned writable output buffer, or zero for a size query.</param>
        /// <param name="outCap">Output capacity in bytes; zero requests a size query.</param>
        /// <param name="outRequired">Receives the required byte count including the final NUL; on failure, this may size an error message.</param>
        /// <returns>True for a successful size query or write; false for insufficient capacity or a native error.</returns>
        /// <remarks>
        /// Deprecated; prefer opencc_convert_cfg_mem_len.
        /// A query converts once to compute the size; a subsequent write converts again. Successful writes append a NUL.
        /// Success clears the thread-local diagnostic. On failure, a sufficiently large buffer may receive error text;
        /// an undersized buffer is unchanged. Read <see cref="opencc_last_error"/> immediately on the calling thread.
        /// Input and output regions must not overlap. The caller owns the output; do not pass it to opencc_string_free.
        /// </remarks>
        [Obsolete("Deprecated native compatibility entry point. Use opencc_convert_cfg_mem_len instead.")]
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool opencc_convert_cfg_mem(
            IntPtr opencc,
            byte[] input,
            int config,
            [MarshalAs(UnmanagedType.I1)] bool punctuation,
            IntPtr outBuf,
            UIntPtr outCap,
            out UIntPtr outRequired);

        /// <summary>
        /// Converts explicit-length UTF-8 input into a caller-owned buffer.
        /// </summary>
        /// <param name="opencc">Pointer to a live native instance, unchanged and not freed during the call.</param>
        /// <param name="input">Borrowed readable UTF-8 bytes; must be nonnull even for zero length. No terminator is required.</param>
        /// <param name="inputLen">Readable input byte count, represented by native size_t and at most PTRDIFF_MAX.</param>
        /// <param name="config">Native opencc_config_t configuration ID.</param>
        /// <param name="punctuation">Whether to apply the selected configuration's punctuation mappings.</param>
        /// <param name="outBuf">Caller-owned writable output buffer, or zero for a size query.</param>
        /// <param name="outCap">Output capacity in bytes; zero requests a size query.</param>
        /// <param name="outRequired">Receives the required byte count including the final NUL; on failure, this may size an error message.</param>
        /// <returns>True for a successful size query or write; false for insufficient capacity or a native error.</returns>
        /// <remarks>
        /// No input terminator is required. Embedded NUL bytes are included in the input length.
        /// A query converts once to compute the size; a subsequent write converts again. Successful writes append a NUL.
        /// Success clears the thread-local diagnostic. On failure, a sufficiently large buffer may receive error text;
        /// an undersized buffer is unchanged. Read <see cref="opencc_last_error"/> immediately on the calling thread.
        /// Input and output regions must not overlap. The caller owns the output; do not pass it to opencc_string_free.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool opencc_convert_cfg_mem_len(
            IntPtr opencc,
            byte[] input,
            UIntPtr inputLen,
            int config,
            [MarshalAs(UnmanagedType.I1)] bool punctuation,
            IntPtr outBuf,
            UIntPtr outCap,
            out UIntPtr outRequired);

        /// <summary>
        /// Looks up a native configuration ID by canonical name.
        /// </summary>
        /// <param name="nameUtf8">Borrowed NUL-terminated UTF-8 configuration name; ASCII case-insensitive.</param>
        /// <param name="configId">Native configuration ID; an output parameter receives the ID on success.</param>
        /// <remarks>
        /// Returns true for a supported name, false otherwise. Native code leaves the output ID unchanged on failure. Does not change the thread-local diagnostic.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool opencc_config_name_to_id(
            byte[] nameUtf8,
            out int configId);

        /// <summary>
        /// Looks up the canonical lowercase name of a native configuration ID.
        /// </summary>
        /// <param name="configId">Native configuration ID; an output parameter receives the ID on success.</param>
        /// <remarks>
        /// Returns a static NUL-terminated UTF-8 pointer or zero for an undefined ID. Do not modify or free it; it remains valid while the library is loaded. Does not change the thread-local diagnostic.
        /// </remarks>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_config_id_to_name(
            int configId);
    }
}