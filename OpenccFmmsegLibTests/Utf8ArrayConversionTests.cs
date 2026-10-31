using System.Text;
using OpenccFmmsegLib;

namespace OpenccFmmsegLibTests;

[TestClass]
public sealed class Utf8ArrayConversionTests
{
    [TestMethod]
    [DataRow("汉字转换", "漢字轉換", false)]
    [DataRow("汉字𠀀😀转换", "漢字𠀀😀轉換", false)]
    [DataRow("“汉字”", "「漢字」", true)]
    public void SuccessReturnsExactUtf8BytesAndOneTerminator(string input, string expected, bool punctuation)
    {
        using var converter = new OpenccFmmseg();
        var result = converter.ConvertCfgToUtf8Z(input, (int)OpenccConfig.S2T, punctuation);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expected + "\0"), result);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void EmptyInputReturnsOwnedSingleNulAndPreservesPriorError(string? input)
    {
        using var converter = new OpenccFmmseg();
        Assert.ThrowsExactly<InvalidOperationException>(() => converter.ConvertCfgToUtf8Z("汉", 9999));
        var priorError = OpenccFmmseg.LastError();
        var first = converter.ConvertCfgToUtf8Z(input!, 9999);
        var second = converter.ConvertCfgToUtf8Z(input!, (int)OpenccConfig.S2T);
        Assert.AreEqual(priorError, OpenccFmmseg.LastError());
        CollectionAssert.AreEqual(new byte[] { 0 }, first);
        CollectionAssert.AreEqual(new byte[] { 0 }, second);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("汉字")]
    public void DisposedInstanceStillThrows(string? input)
    {
        var converter = new OpenccFmmseg();
        converter.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => converter.ConvertCfgToUtf8Z(input!, 1));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(9999)]
    public void InvalidConfigPreservesNativeFailureAndAllowsRecovery(int config)
    {
        using var converter = new OpenccFmmseg();
        Assert.IsFalse(converter.TryConvertCfgToUtf8("汉字", config, false, Span<byte>.Empty, out _));
        var expectedError = OpenccFmmseg.LastError();
        var error = Assert.ThrowsExactly<InvalidOperationException>(
            () => converter.ConvertCfgToUtf8Z("汉字", config));
        Assert.AreEqual(expectedError, error.Message);
        Assert.AreEqual(expectedError, OpenccFmmseg.LastError());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("漢字\0"), converter.ConvertCfgToUtf8Z("汉字", 1));
    }

    [TestMethod]
    [DataRow("\0汉字")]
    [DataRow("汉\0字")]
    [DataRow("汉字\0")]
    public void EmbeddedNulFailsWithoutCStringTruncation(string input)
    {
        using var converter = new OpenccFmmseg();
        Assert.IsFalse(converter.TryConvertCfgToUtf8(input, 1, false, Span<byte>.Empty, out _));
        var expectedError = OpenccFmmseg.LastError();
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => converter.ConvertCfgToUtf8Z(input, 1));
        Assert.AreEqual(expectedError, error.Message);
        Assert.AreEqual(expectedError, OpenccFmmseg.LastError());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("漢字\0"), converter.ConvertCfgToUtf8Z("汉字", 1));
    }

    [TestMethod]
    public void InvalidUtf16RetainsReplacementEncoding()
    {
        using var converter = new OpenccFmmseg();
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("漢\uFFFD字\uFFFD\0"),
            converter.ConvertCfgToUtf8Z("汉\uD800字\uDC00", 1));
    }

    [TestMethod]
    [DataRow("很長的自訂字串𠀀😀")]
    [DataRow("短")]
    public void CustomOutputSizeIsQueriedAndReturnedArrayIsIndependent(string target)
    {
        var spec = new CustomDictSpec(DictSlot.STPhrases, CustomDictMode.Append,
            new[] { KeyValuePair.Create("汉字转换", target) });
        using var converter = new OpenccFmmseg(new[] { spec });
        var first = converter.ConvertCfgToUtf8Z("汉字转换", 1);
        var expected = Encoding.UTF8.GetBytes(target + "\0");
        CollectionAssert.AreEqual(expected, first);
        first[0] = 0;
        CollectionAssert.AreEqual(expected, converter.ConvertCfgToUtf8Z("汉字转换", 1));
    }
}
