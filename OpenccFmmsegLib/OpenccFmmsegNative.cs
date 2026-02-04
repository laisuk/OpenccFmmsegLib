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
        /// Gets the OpenCC-FMMSEG C API ABI version number.
        /// </summary>
        /// <remarks>
        /// This value is intended for <b>runtime binary compatibility checks</b>.
        /// It changes <b>only</b> when the native C ABI is broken (for example,
        /// when function signatures or calling conventions change).
        ///
        /// <para>
        /// Managed bindings (P/Invoke, JNI, ctypes, etc.) should verify this value
        /// before invoking other native functions.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A monotonically increasing ABI version number.
        /// </returns>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint opencc_abi_number();

        /// <summary>
        /// Gets the OpenCC-FMMSEG native library version string.
        /// </summary>
        /// <remarks>
        /// The returned string is a UTF-8, null-terminated version identifier
        /// (for example, <c>"0.8.5"</c>).
        ///
        /// <para>
        /// The returned pointer is owned by the native library and remains valid
        /// for the lifetime of the process. Callers must not free it.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A pointer to a UTF-8 encoded, null-terminated version string.
        /// </returns>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr opencc_version_string();
        
        /// <summary>
        /// Creates a new <c>OpenCC</c> instance in unmanaged memory.
        /// </summary>
        /// <returns>
        /// A pointer to the newly allocated native <c>OpenCC</c> object,
        /// or <see cref="IntPtr.Zero"/> if initialization failed.
        /// </returns>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_new();

        /// <summary>
        /// Deletes a previously created <c>OpenCC</c> instance and releases its resources.
        /// </summary>
        /// <param name="opencc">
        /// Pointer returned from <see cref="opencc_new"/>. Must not be <see cref="IntPtr.Zero"/>.
        /// </param>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opencc_delete(IntPtr opencc);

        /// <summary>
        /// Converts input UTF-8 text using the specified OpenCC configuration and punctuation option.
        /// </summary>
        /// <param name="opencc">Pointer to a valid native <c>OpenCC</c> instance.</param>
        /// <param name="input">UTF-8 encoded byte array of the input string, null-terminated.</param>
        /// <param name="config">UTF-8 encoded null-terminated string representing the conversion config name (e.g. <c>"s2t"</c>).</param>
        /// <param name="punctuation">Whether to convert punctuation marks (<c>true</c>) or leave them unchanged (<c>false</c>).</param>
        /// <returns>
        /// Pointer to a newly allocated UTF-8 null-terminated string containing the converted text.
        /// The caller must free this memory using <see cref="opencc_string_free"/>.
        /// </returns>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_convert(
            IntPtr opencc,
            byte[] input,
            byte[] config,
            [MarshalAs(UnmanagedType.I1)] bool punctuation);

        /// <summary>
        /// Checks the language type of the input Chinese text (Simplified or Traditional).
        /// </summary>
        /// <param name="opencc">Pointer to a valid native <c>OpenCC</c> instance.</param>
        /// <param name="input">UTF-8 encoded null-terminated input string.</param>
        /// <returns>
        /// An integer code defined by the native OpenCC implementation:
        /// <list type="bullet">
        /// <item><description><c>1</c> = Traditional Chinese</description></item>
        /// <item><description><c>2</c> = Simplified Chinese</description></item>
        /// <item><description><c>0</c> = Other or undetermined</description></item>
        /// </list>
        /// </returns>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opencc_zho_check(IntPtr opencc, byte[] input);

        /// <summary>
        /// Frees a UTF-8 string allocated by <see cref="opencc_convert"/>.
        /// </summary>
        /// <param name="str">
        /// Pointer to the unmanaged string previously returned by <see cref="opencc_convert"/>.
        /// Safe to call with <see cref="IntPtr.Zero"/>.
        /// </param>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opencc_string_free(IntPtr str);

        /// <summary>
        /// Retrieves the last error message recorded by the native library.
        /// </summary>
        /// <returns>
        /// Pointer to a UTF-8 null-terminated error string owned by the native side,
        /// or <see cref="IntPtr.Zero"/> if no error is present.
        /// The caller must release it using <see cref="opencc_error_free"/>.
        /// </returns>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_last_error();

        /// <summary>
        /// Frees the error message string returned by <see cref="opencc_last_error"/>.
        /// </summary>
        /// <param name="str">
        /// Pointer to the unmanaged error string to free.
        /// Safe to call with <see cref="IntPtr.Zero"/>.
        /// </param>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opencc_error_free(IntPtr str);

        /// <summary>
        /// Converts input UTF-8 text using a numeric OpenCC config and punctuation option.
        /// </summary>
        /// <param name="opencc">Pointer to a valid native <c>OpenCC</c> instance.</param>
        /// <param name="input">UTF-8 encoded byte array of the input string, null-terminated.</param>
        /// <param name="config">Numeric config value (opencc_config_t).</param>
        /// <param name="punctuation">Whether to convert punctuation marks.</param>
        /// <returns>
        /// Pointer to a newly allocated UTF-8 null-terminated string containing the converted text.
        /// The caller must free this memory using <see cref="opencc_string_free"/>.
        /// </returns>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_convert_cfg(
            IntPtr opencc,
            byte[] input,
            int config,
            [MarshalAs(UnmanagedType.I1)] bool punctuation);

        /// <summary>
        /// Converts a canonical OpenCC config name (e.g. "s2twp") to a numeric config ID.
        /// </summary>
        /// <param name="nameUtf8">
        /// Null-terminated UTF-8 string containing the canonical config name.
        /// </param>
        /// <param name="configId">
        /// Receives the numeric OpenCC config ID on success.
        /// </param>
        /// <returns>
        /// true if the name is valid; false otherwise.
        /// </returns>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool opencc_config_name_to_id(
            byte[] nameUtf8,
            out int configId);

        /// <summary>
        /// Converts a numeric OpenCC config ID to its canonical lowercase name.
        /// </summary>
        /// <param name="configId">Numeric OpenCC config ID.</param>
        /// <returns>
        /// Pointer to a static UTF-8 string, or IntPtr.Zero if invalid.
        /// </returns>
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opencc_config_id_to_name(
            int configId);
    }
}