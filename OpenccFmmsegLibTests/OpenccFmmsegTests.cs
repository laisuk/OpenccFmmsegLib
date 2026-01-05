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

    // ----------------------------
    // NEW TESTS: ConvertCfgCore
    // ----------------------------

    [TestMethod]
    public void ConvertCfg_s2t_Test()
    {
        // S2T expected numeric = 0 (per your enum)
        var result = _opencc.ConvertCfg("龙马精神", OpenccConfig.S2T);
        Assert.AreEqual("龍馬精神", result);
    }

    [TestMethod]
    public void ConvertCfg_Enum_s2twp_Test()
    {
        var result = _opencc.ConvertCfg("这是一项意大利商务项目", OpenccConfig.S2TWP);
        Assert.AreEqual("這是一項義大利商務專案", result);
    }

    [TestMethod]
    public void ConvertCfg_WithPunct_Test()
    {
        var result = _opencc.ConvertCfg("“龙马精神”", OpenccConfig.S2TWP, punctuation: true);
        Assert.AreEqual("「龍馬精神」", result);
    }

    // [TestMethod]
    // public void ConvertCfg_InvalidConfig_ReturnsErrorString_AndSetsLastError()
    // {
    //     // Intentionally invalid
    //     const int invalidCfg = 999999;
    //
    //     var result = _opencc.ConvertCfgCore("龙马精神", invalidCfg);
    //
    //     // Contract: should return an allocated error string, not NULL (unless OOM/NULL inputs)
    //     Assert.IsFalse(string.IsNullOrEmpty(result));
    //
    //     // Expect format: "Invalid config: <value>"
    //     // Use Contains instead of strict equals to avoid minor wording differences.
    //     Assert.Contains("Invalid config", result);
    //     Assert.Contains(invalidCfg.ToString(), result);
    //
    //     // Also stored internally and retrievable via opencc_last_error()
    //     var last = OpenccFmmseg.LastError();
    //     Assert.Contains("Invalid config", last);
    //     Assert.Contains(invalidCfg.ToString(), last);
    // }

    [TestMethod]
    public void ConvertCfg_NullOrEmptyInput_ReturnsEmptyString()
    {
        // Matches existing Convert() early return behavior
        var r1 = _opencc.ConvertCfg("", OpenccConfig.S2T);
        Assert.AreEqual(string.Empty, r1);

        // If allow null inputs in signature, uncomment this:
        var r2 = _opencc.ConvertCfg(null, OpenccConfig.S2T);
        Assert.AreEqual(string.Empty, r2);
    }

    [TestMethod]
    public void ToCanonicalName_ReturnsExpectedCanonicalNames()
    {
        Assert.AreEqual("s2t", OpenccConfig.S2T.ToCanonicalName());
        Assert.AreEqual("s2tw", OpenccConfig.S2TW.ToCanonicalName());
        Assert.AreEqual("s2twp", OpenccConfig.S2TWP.ToCanonicalName());
        Assert.AreEqual("s2hk", OpenccConfig.S2HK.ToCanonicalName());

        Assert.AreEqual("t2s", OpenccConfig.T2S.ToCanonicalName());
        Assert.AreEqual("t2tw", OpenccConfig.T2TW.ToCanonicalName());
        Assert.AreEqual("t2twp", OpenccConfig.T2TWP.ToCanonicalName());
        Assert.AreEqual("t2hk", OpenccConfig.T2HK.ToCanonicalName());

        Assert.AreEqual("tw2s", OpenccConfig.TW2S.ToCanonicalName());
        Assert.AreEqual("tw2sp", OpenccConfig.TW2SP.ToCanonicalName());
        Assert.AreEqual("tw2t", OpenccConfig.TW2T.ToCanonicalName());
        Assert.AreEqual("tw2tp", OpenccConfig.TW2TP.ToCanonicalName());

        Assert.AreEqual("hk2s", OpenccConfig.HK2S.ToCanonicalName());
        Assert.AreEqual("hk2t", OpenccConfig.HK2T.ToCanonicalName());

        Assert.AreEqual("jp2t", OpenccConfig.JP2T.ToCanonicalName());
        Assert.AreEqual("t2jp", OpenccConfig.T2JP.ToCanonicalName());
    }
}