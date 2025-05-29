using System.Diagnostics;
using OpenccFmmsegLib;

namespace OpenccFmmsegLibTests;

[TestClass]
public sealed class OpenccFmmsegTests
{
    private readonly OpenccFmmseg _opencc = new();

    [TestMethod]
    public void Convert_Test()
    {
        var result = _opencc.Convert("龙马精神", "s2t");
        Assert.AreEqual("龍馬精神", result);
    }

    [TestMethod]
    public void Convert_s2twp_Test()
    {
        var result = _opencc.Convert("这是一项意大利商务项目", "s2twp");
        Assert.AreEqual("這是一項義大利商務專案", result);
    }

    [TestMethod]
    public void ConvertWithPunct_Test()
    {
        var result = _opencc.Convert("“龙马精神”", "s2tw", true);
        Assert.AreEqual("「龍馬精神」", result);
    }

    [TestMethod]
    public void Change_Conversion_Test()
    {
        var result = _opencc.Convert("龙马精神", "s2t");
        Assert.AreEqual("龍馬精神", result);
        var result1 = _opencc.Convert("龍馬精神", "t2s");
        Assert.AreEqual("龙马精神", result1);
    }

    [TestMethod]
    public void ZhoCheck_Test()
    {
        var result = _opencc.ZhoCheck("龙马精神");
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void last_error_Test()
    {
        var result = OpenccFmmseg.LastError();
        Trace.Assert(result is "No error" or "");
    }
}