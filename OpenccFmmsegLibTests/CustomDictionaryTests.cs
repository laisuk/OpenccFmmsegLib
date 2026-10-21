using System.Runtime.InteropServices;
using System.Text;
using OpenccFmmsegLib;

namespace OpenccFmmsegLibTests;

[TestClass]
public sealed class CustomDictionaryTests
{
    private static CustomDictSpec Spec(DictSlot slot = DictSlot.STPhrases,
        CustomDictMode mode = CustomDictMode.Append, string source = "汉字转换", string target = "自訂轉換") =>
        new(slot, mode, [KeyValuePair.Create(source, target)]);

    [TestMethod]
    public void DefaultAndEmptySpecificationsUseBuiltInDictionaries()
    {
        using var standard = new OpenccFmmseg();
        using var custom = new OpenccFmmseg([]);
        Assert.AreEqual("龍馬精神", standard.Convert("龙马精神", "s2t"));
        Assert.AreEqual(standard.Convert("汉字转换龙马精神", "s2t"), custom.Convert("汉字转换龙马精神", "s2t"));
    }

    [TestMethod]
    public void AppendAddsMappingsAndReplacesExistingKeys()
    {
        using var converter = new OpenccFmmseg([Spec(), Spec(source: "龙马精神", target: "自訂詞語")]);
        Assert.AreEqual("自訂轉換自訂詞語漢語", converter.Convert("汉字转换龙马精神汉语", "s2t"));
    }

    [TestMethod]
    public void OverrideReplacesOnlyTheSelectedSlot()
    {
        using var converter = new OpenccFmmseg([Spec(DictSlot.STCharacters, CustomDictMode.Override, "汉", "訂")]);
        Assert.AreEqual("訂龙", converter.Convert("汉龙", "s2t"));
        Assert.AreEqual("龙", converter.Convert("龍", "t2s"));
    }

    [TestMethod]
    public void MultipleSlotsAndEntriesAreCopiedAndIndependent()
    {
        var entries = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create("汉字转换", "自訂轉換"),
            KeyValuePair.Create("测试词语", "自訂詞語")
        };
        var snapshot = new CustomDictSpec(DictSlot.STPhrases, CustomDictMode.Append, entries);
        entries.Clear();
        Assert.HasCount(2, snapshot.Entries);
        var specs = new List<CustomDictSpec> { snapshot, Spec(DictSlot.TSPhrases, source: "自訂詞語", target: "自定义") };
        using var converter = new OpenccFmmseg(specs);
        specs.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.AreEqual("自訂轉換自訂詞語", converter.Convert("汉字转换测试词语", "s2t"));
        Assert.AreEqual("自定义", converter.Convert("自訂詞語", "t2s"));
    }

    [TestMethod]
    public void DuplicateKeysAndRepeatedSlotsUseInputOrder()
    {
        var duplicates = new CustomDictSpec(DictSlot.STPhrases, CustomDictMode.Append, [
            KeyValuePair.Create("测试词语", "第一個"),
            KeyValuePair.Create("测试词语", "最後一個")
        ]);
        using var converter = new OpenccFmmseg([
            duplicates, Spec(target: "先前"),
            Spec(mode: CustomDictMode.Override, target: "覆蓋"), Spec(source: "另一词语", target: "追加")
        ]);
        Assert.AreEqual("覆蓋追加", converter.Convert("汉字转换另一词语", "s2t"));
        using var duplicateConverter = new OpenccFmmseg([duplicates]);
        Assert.AreEqual("最後一個", duplicateConverter.Convert("测试词语", "s2t"));
    }

    [TestMethod]
    public void SupplementaryUnicodeRoundTripsThroughUtf8()
    {
        using var converter = new OpenccFmmseg([Spec(source: "𠀀😀", target: "𠮷🚀")]);
        Assert.AreEqual("𠮷🚀", converter.Convert("𠀀😀", "s2t"));
    }

    [TestMethod]
    [DataRow(CustomDictMode.Append, "龍")]
    [DataRow(CustomDictMode.Override, "龙")]
    public void EmptyDictionaryFollowsNativeModeSemantics(CustomDictMode mode, string expected)
    {
        using var converter = new OpenccFmmseg([
            new CustomDictSpec(DictSlot.STCharacters, mode, [])
        ]);
        Assert.AreEqual(expected, converter.Convert("龙", "s2t"));
    }

    [TestMethod]
    public void AllConversionPathsUseCustomMappings()
    {
        using var converter = new OpenccFmmseg([Spec()]);
        const string input = "汉字转换";
        const string expected = "自訂轉換";
        Assert.AreEqual(expected, converter.Convert(input, "s2t"));
        Assert.AreEqual(expected, converter.Convert(input, OpenccConfig.S2T));
        Assert.AreEqual(expected, converter.ConvertCfg(input, OpenccConfig.S2T));
        Assert.AreEqual(expected, converter.ConvertCfg(input, (int)OpenccConfig.S2T));
#pragma warning disable CS0618 // Verify that existing compatibility aliases also honor custom dictionaries.
        Assert.AreEqual(expected, converter.ConvertCfgMemLen(input, (int)OpenccConfig.S2T));
        var utf8 = converter.ConvertCfgToUtf8Z(input, (int)OpenccConfig.S2T);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(utf8, 0, utf8.Length - 1));
        utf8 = converter.ConvertCfgMemLenToUtf8Z(input, (int)OpenccConfig.S2T);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(utf8, 0, utf8.Length - 1));
        var destination = new byte[100];
        Assert.IsTrue(converter.TryConvertCfgToUtf8(input, (int)OpenccConfig.S2T, false, destination,
            out var required));
        Assert.AreEqual(expected, Encoding.UTF8.GetString(destination, 0, required - 1));
        Assert.IsTrue(
            converter.TryConvertCfgToUtf8Into(input, (int)OpenccConfig.S2T, false, destination, out required));
        Assert.AreEqual(expected, Encoding.UTF8.GetString(destination, 0, required - 1));
#pragma warning restore CS0618
        Assert.AreEqual(2, converter.ZhoCheck("龙马精神"));
    }

    [TestMethod]
    public void InstancesRemainIsolatedAcrossConstructionAndDisposal()
    {
        using var standard = new OpenccFmmseg();
        for (var i = 0; i < 10; i++)
        {
            using var first = new OpenccFmmseg([Spec(target: "第一個")]);
            var second = new OpenccFmmseg([Spec(target: "第二個")]);
            Assert.AreEqual("第一個", first.Convert("汉字转换", "s2t"));
            Assert.AreEqual("第二個", second.Convert("汉字转换", "s2t"));
            second.Dispose();
            second.Dispose();
            AssertDisposedConverter(second);
            Assert.AreEqual("第一個", first.Convert("汉字转换", "s2t"));
        }

        Assert.AreEqual("漢字轉換", standard.Convert("汉字转换", "s2t"));

        static void AssertDisposedConverter(OpenccFmmseg instance)
        {
            Assert.ThrowsExactly<ObjectDisposedException>(() => instance.Convert("", "s2t"));
        }
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(26u)]
    [DataRow(uint.MaxValue)]
    public void InvalidSlotsAreRejected(uint slot) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Spec((DictSlot)slot));

    [TestMethod]
    [DataRow(0u)]
    [DataRow(3u)]
    [DataRow(uint.MaxValue)]
    public void InvalidModesAreRejected(uint mode) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Spec(mode: (CustomDictMode)mode));

    [TestMethod]
    [DataRow("a\0b", "target")]
    [DataRow("source", "a\0b")]
    [DataRow(null, "target")]
    [DataRow("source", null)]
    public void InvalidStringsAreRejected(string source, string target) =>
        Assert.ThrowsExactly<ArgumentException>(() => Spec(source: source, target: target));

    [TestMethod]
    public void UnpairedSurrogatesAreRejected() =>
        Assert.ThrowsExactly<EncoderFallbackException>(() => Spec(source: "\ud800"));

    [TestMethod]
    public void NullCollectionsAndSpecificationsAreRejected()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new OpenccFmmseg(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new CustomDictSpec(DictSlot.STPhrases, CustomDictMode.Append, null!));
        Assert.ThrowsExactly<ArgumentException>(() => new OpenccFmmseg([Spec(), null!]));
    }

    [TestMethod]
    public void NativeStructuresMatchHeaderLayout()
    {
        Assert.AreEqual(2 * IntPtr.Size, Marshal.SizeOf<OpenccFmmsegNative.CustomPair>());
        Assert.AreEqual(IntPtr.Size, (int)Marshal.OffsetOf<OpenccFmmsegNative.CustomPair>("Target"));
        Assert.AreEqual(8 + 2 * IntPtr.Size, Marshal.SizeOf<OpenccFmmsegNative.CustomDictionarySpecification>());
        Assert.AreEqual(4, (int)Marshal.OffsetOf<OpenccFmmsegNative.CustomDictionarySpecification>("Mode"));
        Assert.AreEqual(8, (int)Marshal.OffsetOf<OpenccFmmsegNative.CustomDictionarySpecification>("Pairs"));
        Assert.AreEqual(8 + IntPtr.Size,
            (int)Marshal.OffsetOf<OpenccFmmsegNative.CustomDictionarySpecification>("PairCount"));
    }

    [TestMethod]
    public void SlotIdsMatchTheCheckedInHeader()
    {
        var names = new[]
        {
            "STCharacters", "STPhrases", "TSCharacters", "TSPhrases", "TWPhrases", "TWPhrasesRev",
            "HKPhrases", "HKPhrasesRev", "TWVariants", "TWVariantsPhrases", "TWVariantsRev", "TWVariantsRevPhrases",
            "HKVariants", "HKVariantsPhrases", "HKVariantsRev", "HKVariantsRevPhrases", "JPSCharacters",
            "JPSCharactersRev",
            "JPSPhrases", "STPunctuations", "TSPunctuations", "SealCharacters", "SealCharactersRev", "SealVariants",
            "SealVariantsRev"
        };
        Assert.HasCount(25, Enum.GetValues<DictSlot>());
        for (var i = 0; i < names.Length; i++)
            Assert.AreEqual((uint)(i + 1), (uint)Enum.Parse<DictSlot>(names[i]));
        Assert.AreEqual(1u, (uint)CustomDictMode.Append);
        Assert.AreEqual(2u, (uint)CustomDictMode.Override);
    }

    [TestMethod]
    public void EverySupportedSlotCanBePassedTogetherToNativeConstructor()
    {
        var supportsSeal = Version.Parse(OpenccFmmseg.GetNativeVersionString()) >= new Version(0, 13, 0);
        var specs = Enum.GetValues<DictSlot>()
            .Where(slot => supportsSeal || (uint)slot <= 21)
            .Select(slot => new CustomDictSpec(slot, CustomDictMode.Append, []));
        using var converter = new OpenccFmmseg(specs);
        Assert.AreEqual("龍馬精神", converter.Convert("龙马精神", "s2t"));
    }

    [TestMethod]
    public void NativeConstructorRejectsNullSpecificationsWithNonzeroCount()
    {
        var instance = OpenccFmmsegNative.opencc_new_custom(IntPtr.Zero, new UIntPtr(1));
        var error = OpenccFmmseg.LastError();
        try
        {
            Assert.AreEqual(IntPtr.Zero, instance);
            Assert.Contains("specs is NULL", error);
        }
        finally
        {
            if (instance != IntPtr.Zero) OpenccFmmsegNative.opencc_delete(instance);
        }
    }

    [TestMethod]
    [DataRow(DictSlot.SealCharacters, OpenccConfig.SEAL2T, "𠀀", "訂")]
    [DataRow(DictSlot.SealCharactersRev, OpenccConfig.T2SEAL, "訂", "𠀀")]
    [DataRow(DictSlot.SealVariants, OpenccConfig.T2SEAL, "😀", "🚀")]
    [DataRow(DictSlot.SealVariantsRev, OpenccConfig.SEAL2T, "𠀀", "訂")]
    public void SealSlotsFollowLoadedNativeVersion(DictSlot slot, OpenccConfig config, string source, string target)
    {
        var specs = new[] { Spec(slot, source: source, target: target) };
        if (Version.Parse(OpenccFmmseg.GetNativeVersionString()) < new Version(0, 13, 0))
        {
            var error = Assert.ThrowsExactly<InvalidOperationException>(() => new OpenccFmmseg(specs));
            Assert.Contains("slot", error.Message);
            return;
        }

        using var converter = new OpenccFmmseg(specs);
        Assert.AreEqual(target, converter.Convert(source, config));
        Assert.AreEqual(target, converter.Convert(source, config.ToCanonicalName()));
        Assert.AreEqual(target, converter.ConvertCfg(source, config));
    }

    [TestMethod]
    [DataRow(0, "slot")]
    [DataRow(4, "mode")]
    public void NativeFailurePreservesErrorAndReleasesBorrowedBuffers(int fieldOffset, string expectedError)
    {
        // Bypass public enum validation to exercise the actual native-failure and managed exception path.
        var buffers = new CustomDictionaryMarshaller([Spec()]);
        try
        {
            Marshal.WriteInt32(buffers.Specifications, fieldOffset, 999);
            AssertNativeFailure(buffers, expectedError);
        }
        finally
        {
            buffers.Dispose();
        }

        Assert.AreEqual(IntPtr.Zero, buffers.Specifications);

        using var recovered = new OpenccFmmseg([Spec()]);
        Assert.AreEqual("自訂轉換", recovered.Convert("汉字转换", "s2t"));

        static void AssertNativeFailure(CustomDictionaryMarshaller marshaller, string expectedError)
        {
            var error = Assert.ThrowsExactly<InvalidOperationException>(() => marshaller.CreateInstance());
            Assert.Contains("Failed to initialize OpenCC with custom dictionaries", error.Message);
            Assert.Contains(expectedError, error.Message);
            Assert.Contains("999", error.Message);
        }
    }
}
