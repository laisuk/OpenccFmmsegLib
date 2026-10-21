using System;
using System.Collections.Generic;
using System.Text;

namespace OpenccFmmsegLib
{
    /// <summary>An immutable, ordered snapshot of custom mappings for one dictionary slot.</summary>
    /// <remarks>
    /// Specifications are applied in constructor order. Duplicate source keys use the last mapping.
    /// Entries are literal source/target strings; they are not parsed as dictionary file rows.
    /// Empty dictionaries are permitted: append leaves the slot unchanged, while override clears it.
    /// Empty strings are passed through according to native dictionary semantics.
    /// </remarks>
    public sealed class CustomDictSpec
    {
        /// <summary>Gets the native dictionary slot to customize.</summary>
        public DictSlot Slot { get; }

        /// <summary>Gets the merge mode for this specification.</summary>
        public CustomDictMode Mode { get; }

        /// <summary>Gets the read-only snapshot of source/target mappings in input order.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Entries { get; }

        /// <summary>Creates a custom dictionary specification and copies its entries.</summary>
        /// <param name="slot">A defined native dictionary slot.</param>
        /// <param name="mode">Append or override behavior for the selected slot.</param>
        /// <param name="entries">Ordered source/target mappings; may be empty but cannot be null.</param>
        /// <exception cref="ArgumentOutOfRangeException">The slot or mode is undefined.</exception>
        /// <exception cref="ArgumentNullException">The entries collection is null.</exception>
        /// <exception cref="ArgumentException">An entry contains null, an embedded NUL, or invalid UTF-16.</exception>
        public CustomDictSpec(DictSlot slot, CustomDictMode mode,
            IEnumerable<KeyValuePair<string, string>> entries)
        {
            if (!Enum.IsDefined(typeof(DictSlot), slot))
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown dictionary slot.");
            if (!Enum.IsDefined(typeof(CustomDictMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown custom dictionary mode.");
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            var snapshot = new List<KeyValuePair<string, string>>();
            var strictUtf8 = new UTF8Encoding(false, true);
            foreach (var entry in entries)
            {
                ValidateString(entry.Key, strictUtf8);
                ValidateString(entry.Value, strictUtf8);
                snapshot.Add(entry);
            }

            Slot = slot;
            Mode = mode;
            Entries = snapshot.AsReadOnly();
        }

        private static void ValidateString(string value, Encoding encoding)
        {
            if (value == null || value.IndexOf('\0') >= 0)
                throw new ArgumentException("Dictionary sources and targets must be non-null and contain no NUL characters.", nameof(value));
            // Do not silently replace unpaired UTF-16 surrogates during UTF-8 encoding.
            _ = encoding.GetByteCount(value);
        }
    }
}
