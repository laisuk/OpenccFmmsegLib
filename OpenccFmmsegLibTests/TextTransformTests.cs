using System.Runtime.InteropServices;
using System.Text;
using OpenccFmmsegLib;

namespace OpenccFmmsegLibTests;

[TestClass]
public sealed class TextTransformTests
{
    [TestMethod]
    [DataRow("金庸", "金庸")]
    [DataRow("龜龜樂天", "龜龜樂天")]
    [DataRow("天龍八部書", "天龍八部書")]
    [DataRow("鼖鼻𪘀", "鼖鼻𪘀")]
    public void BasicNormalizationMatchesNativeAndJavaGoldenCases(string input, string expected)
    {
        // Cases from native compat_ideographs.rs/C API tests and OpenccJava OpenCCTest.java.
        using var converter = new OpenccFmmseg();
        Assert.AreEqual(expected, converter.NormalizeCompat(input));
    }

    [TestMethod]
    public void ExtendedNormalizationAddsCuratedMappings()
    {
        using var converter = new OpenccFmmseg();
        const string input = "天龍八部書裡的聼眾‧聼聼竒羙⽟䂖甁噐⾳";
        // Exact golden result from the native C API test.
        const string expected = "天龍八部書裡的聽眾·聽聽奇美玉石瓶器音";
        Assert.AreEqual(expected, converter.NormalizeCompatExtended(input));
        Assert.AreEqual("聼竒羙䂖甁噐", converter.NormalizeCompat("聼竒羙䂖甁噐"));
        Assert.AreEqual("聽奇美石瓶器", converter.NormalizeCompatExtended("聼竒羙䂖甁噐"));
        Assert.AreEqual("天龙八部书里的听众·听听奇美玉石瓶器音", converter.Convert(expected, "t2s"));
    }

    [TestMethod]
    public void UnmappedBmpAndSupplementaryCharactersArePreserved()
    {
        using var converter = new OpenccFmmseg();
        const string input = "普通中文 ABC 123，﨑😀𱁬";
        // Native tests identify U+FA11 as unmapped; Java tests identify 𱁬 as lacking a DeTofu fallback.
        Assert.AreEqual(input, converter.NormalizeCompat(input));
        Assert.AreEqual(input, converter.NormalizeCompatExtended(input));
        foreach (var level in Enum.GetValues<DeTofuLevel>())
            Assert.AreEqual(input, converter.DeTofu(input, level));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void NullAndEmptyInputReturnEmptyStrings(string? input)
    {
        using var converter = new OpenccFmmseg();
        Assert.AreEqual("", converter.NormalizeCompat(input!));
        Assert.AreEqual("", converter.NormalizeCompatExtended(input!));
        foreach (var level in Enum.GetValues<DeTofuLevel>())
            Assert.AreEqual("", converter.DeTofu(input!, level));
    }

    [TestMethod]
    [DataRow(DeTofuLevel.ExtB, "儣𠿕㑮僤繐")]
    [DataRow(DeTofuLevel.ExtC, "𠆲𠿕㑮僤繐")]
    [DataRow(DeTofuLevel.ExtD, "𠆲𪜎㑮僤繐")]
    [DataRow(DeTofuLevel.ExtE, "𠆲𪜎𫝈僤繐")]
    [DataRow(DeTofuLevel.ExtF, "𠆲𪜎𫝈𫢸繐")]
    [DataRow(DeTofuLevel.ExtG, "𠆲𪜎𫝈𫢸繐")]
    [DataRow(DeTofuLevel.ExtH, "𠆲𪜎𫝈𫢸𰬸")]
    [DataRow(DeTofuLevel.ExtI, "𠆲𪜎𫝈𫢸𰬸")]
    public void InclusiveThresholdsMatchNativeBuiltInTable(DeTofuLevel level, string expected)
    {
        // B/C/D/E/G samples come directly from native src/data/CharactersTofu.txt.
        // That table has no F/H/I entries; F includes the later G mappings, while H/I leave these samples unchanged.
        using var converter = new OpenccFmmseg();
        Assert.AreEqual(expected, converter.DeTofu("𠆲𪜎𫝈𫢸𰬸", level));
    }

    [TestMethod]
    public void KnownFallbackAndMixedUnicodeMatchNativeTests()
    {
        using var converter = new OpenccFmmseg();
        Assert.AreEqual("骖騑", converter.DeTofu("骖𬴂", DeTofuLevel.ExtB));
        Assert.AreEqual("這隻小狗有氄毛", converter.DeTofu("這隻小狗有𣭲毛", DeTofuLevel.ExtB));
        Assert.AreEqual("A😀金庸𱁬騑", converter.DeTofu(converter.NormalizeCompat("A😀金庸𱁬𬴂"), DeTofuLevel.ExtB));
    }

    [TestMethod]
    [DataRow(8u)]
    [DataRow(99u)]
    [DataRow(uint.MaxValue)]
    public void UndefinedLevelsAreRejectedEvenForEmptyInput(uint value)
    {
        using var converter = new OpenccFmmseg();
        foreach (var input in new[] { "骖𬴂", "", null })
            AssertInvalidLevel(converter, input, value);

        static void AssertInvalidLevel(OpenccFmmseg instance, string? input, uint value)
        {
            var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => instance.DeTofu(input!, (DeTofuLevel)value));
            Assert.AreEqual("level", error.ParamName);
        }
    }

    [TestMethod]
    public void LevelValuesMatchNativeUint32Abi()
    {
        Assert.AreEqual(typeof(uint), Enum.GetUnderlyingType(typeof(DeTofuLevel)));
        Assert.AreEqual(0u, (uint)DeTofuLevel.ExtB);
        Assert.AreEqual(1u, (uint)DeTofuLevel.ExtC);
        Assert.AreEqual(2u, (uint)DeTofuLevel.ExtD);
        Assert.AreEqual(3u, (uint)DeTofuLevel.ExtE);
        Assert.AreEqual(4u, (uint)DeTofuLevel.ExtF);
        Assert.AreEqual(5u, (uint)DeTofuLevel.ExtG);
        Assert.AreEqual(6u, (uint)DeTofuLevel.ExtH);
        Assert.AreEqual(7u, (uint)DeTofuLevel.ExtI);
        Assert.HasCount(8, Enum.GetValues<DeTofuLevel>());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("金𬴂")]
    public void AllOperationsRejectDisposedInstances(string? input)
    {
        var converter = new OpenccFmmseg();
        converter.Dispose();
        AssertDisposedOperations(converter, input);

        static void AssertDisposedOperations(OpenccFmmseg instance, string? text)
        {
            Assert.ThrowsExactly<ObjectDisposedException>(() => instance.NormalizeCompat(text!));
            Assert.ThrowsExactly<ObjectDisposedException>(() => instance.NormalizeCompatExtended(text!));
            Assert.ThrowsExactly<ObjectDisposedException>(() => instance.DeTofu(text!, DeTofuLevel.ExtB));
        }
    }

    [TestMethod]
    public void EmbeddedNulIsRejectedWithoutTruncation()
    {
        using var converter = new OpenccFmmseg();
        AssertEmbeddedNulRejected(converter);

        static void AssertEmbeddedNulRejected(OpenccFmmseg instance)
        {
            Assert.ThrowsExactly<ArgumentException>(() => instance.NormalizeCompat("金\0庸"));
            Assert.ThrowsExactly<ArgumentException>(() => instance.NormalizeCompatExtended("金\0庸"));
            Assert.ThrowsExactly<ArgumentException>(() => instance.DeTofu("骖\0𬴂", DeTofuLevel.ExtB));
        }
    }

    [TestMethod]
    public void RepeatedOperationsReuseInstanceWithoutChangingConversionOrCustomDictionaries()
    {
        using var converter = new OpenccFmmseg(new[]
        {
            new CustomDictSpec(DictSlot.STPhrases, CustomDictMode.Append,
                new Dictionary<string, string> { ["自定义词语"] = "自訂詞語" })
        });
        for (var i = 0; i < 100; i++)
        {
            Assert.AreEqual("金庸", converter.NormalizeCompat("金庸"));
            Assert.AreEqual("金庸聽", converter.NormalizeCompatExtended("金庸聼"));
            Assert.AreEqual("骖騑", converter.DeTofu("骖𬴂", DeTofuLevel.ExtB));
        }
        Assert.AreEqual("自訂詞語", converter.Convert("自定义词语", "s2t"));
        // Normalization is opt-in: Convert alone must preserve the original compatibility character.
        Assert.AreEqual("金", converter.Convert("金", "s2t"));
        Assert.AreEqual("金", converter.Convert(converter.NormalizeCompat("金"), "s2t"));
    }

    [TestMethod]
    public void SuccessfulTransformsClearPriorNativeErrors()
    {
        using var converter = new OpenccFmmseg();
        for (var operation = 0; operation < 3; operation++)
        {
            var failed = OpenccFmmsegNative.opencc_normalize_compat(IntPtr.Zero, Encoding.UTF8.GetBytes("x\0"));
            var nativeError = OpenccFmmseg.LastError();
            try
            {
                Assert.AreEqual(IntPtr.Zero, failed);
                Assert.Contains("instance/input is NULL", nativeError);
            }
            finally
            {
                if (failed != IntPtr.Zero) OpenccFmmsegNative.opencc_string_free(failed);
            }
            var result = operation switch
            {
                0 => converter.NormalizeCompat("金"),
                1 => converter.NormalizeCompatExtended("金"),
                _ => converter.DeTofu("金", DeTofuLevel.ExtB)
            };
            Assert.AreEqual("金", result);
            Assert.AreEqual("No error", OpenccFmmseg.LastError());
        }
    }

    [TestMethod]
    public void NativeAllocatedErrorStringsUseTheSameOwnershipAsSuccessfulResults()
    {
        var instance = OpenccFmmsegNative.opencc_new();
        Assert.AreNotEqual(IntPtr.Zero, instance);
        try
        {
            foreach (var operation in new[]
                     {
                         () => OpenccFmmsegNative.opencc_normalize_compat(instance, new byte[] { 0xff, 0 }),
                         () => OpenccFmmsegNative.opencc_normalize_compat_extended(instance, new byte[] { 0xff, 0 }),
                         () => OpenccFmmsegNative.opencc_detofu(instance, new byte[] { 0xff, 0 }, 0),
                         () => OpenccFmmsegNative.opencc_detofu(instance, Encoding.UTF8.GetBytes("骖𬴂\0"), 99)
                     })
            {
                var output = operation();
                try
                {
                    var error = OpenccFmmseg.LastError();
                    Assert.AreNotEqual(IntPtr.Zero, output);
                    Assert.AreEqual(error, Marshal.PtrToStringUTF8(output));
                    Assert.IsTrue(error is "Invalid UTF-8 input" or "Invalid DeTofu level: 99");
                }
                finally
                {
                    if (output != IntPtr.Zero) OpenccFmmsegNative.opencc_string_free(output);
                }
            }
        }
        finally
        {
            OpenccFmmsegNative.opencc_delete(instance);
        }
    }
}
