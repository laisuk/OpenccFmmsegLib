namespace OpenccFmmsegLib
{
    // ReSharper disable InconsistentNaming
    
    /// <summary>Native dictionary slots, with explicit IDs from <c>opencc_fmmseg_capi.h</c>.</summary>
    /// <remarks>
    /// Select the slot used by the desired conversion pipeline; changing a slot does not select a configuration.
    /// Slots 1–21 require native v0.11.5 or later. Seal slots require v0.13.0 or later.
    /// All bundled native v0.13.1 runtimes support all 25 slots.
    /// When using an older custom native library, an unsupported slot causes initialization to fail
    /// with an <see cref="System.InvalidOperationException"/>.
    /// </remarks>
    public enum DictSlot : uint
    {
        /// <summary>Simplified to Traditional characters.</summary>
        STCharacters = 1,
        /// <summary>Simplified to Traditional phrases.</summary>
        STPhrases = 2,
        /// <summary>Traditional to Simplified characters.</summary>
        TSCharacters = 3,
        /// <summary>Traditional to Simplified phrases.</summary>
        TSPhrases = 4,
        /// <summary>Traditional to Taiwan phrases.</summary>
        TWPhrases = 5,
        /// <summary>Taiwan to Traditional reverse phrases.</summary>
        TWPhrasesRev = 6,
        /// <summary>Traditional to Hong Kong phrases.</summary>
        HKPhrases = 7,
        /// <summary>Hong Kong to Traditional reverse phrases.</summary>
        HKPhrasesRev = 8,
        /// <summary>Traditional to Taiwan variants.</summary>
        TWVariants = 9,
        /// <summary>Traditional to Taiwan phrase variants.</summary>
        TWVariantsPhrases = 10,
        /// <summary>Taiwan to Traditional reverse variants.</summary>
        TWVariantsRev = 11,
        /// <summary>Taiwan to Traditional reverse phrase variants.</summary>
        TWVariantsRevPhrases = 12,
        /// <summary>Traditional to Hong Kong variants.</summary>
        HKVariants = 13,
        /// <summary>Traditional to Hong Kong phrase variants.</summary>
        HKVariantsPhrases = 14,
        /// <summary>Hong Kong to Traditional reverse variants.</summary>
        HKVariantsRev = 15,
        /// <summary>Hong Kong to Traditional reverse phrase variants.</summary>
        HKVariantsRevPhrases = 16,
        /// <summary>Japanese Shinjitai characters.</summary>
        JPSCharacters = 17,
        /// <summary>Japanese Shinjitai reverse characters.</summary>
        JPSCharactersRev = 18,
        /// <summary>Japanese Shinjitai phrases.</summary>
        JPSPhrases = 19,
        /// <summary>Simplified to Traditional punctuation.</summary>
        STPunctuations = 20,
        /// <summary>Traditional to Simplified punctuation.</summary>
        TSPunctuations = 21,
        /// <summary>Small Seal Script to Traditional characters. Requires native v0.13.0 or later.</summary>
        SealCharacters = 22,
        /// <summary>Traditional to Small Seal Script characters. Requires native v0.13.0 or later.</summary>
        SealCharactersRev = 23,
        /// <summary>Traditional to Small Seal Script variants. Requires native v0.13.0 or later.</summary>
        SealVariants = 24,
        /// <summary>Small Seal Script to Traditional reverse variants. Requires native v0.13.0 or later.</summary>
        SealVariantsRev = 25
    }
    
    // ReSharper restore InconsistentNaming
}
