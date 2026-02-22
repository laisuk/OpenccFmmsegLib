using System.Diagnostics;
using System.Text;
// using System.Reflection;
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
        var result = _opencc.Convert("龙马精神", OpenccConfig.S2T);
        Assert.AreEqual("龍馬精神", result);
        var result1 = _opencc.Convert("龍馬精神", OpenccConfig.T2S);
        Assert.AreEqual("龙马精神", result1);
    }

    [TestMethod]
    public void ZhoCheck_Test()
    {
        var result = _opencc.ZhoCheck("龙马精神");
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    [DoNotParallelize]
    public void last_error_Test()
    {
        var result = OpenccFmmseg.LastError();
        Trace.Assert(result is "No error" or "");
    }

    // ----------------------------
    // NEW TESTS: ConvertCfg
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

    // NOTE:
    // opencc_last_error() reflects process-wide shared state.
    // Under parallel test execution, another test may overwrite the last error
    // between ConvertCfg(...) and LastError(), causing rare, non-deterministic failures.
    // This is expected behavior and not a correctness issue.
    [TestMethod]
    [DoNotParallelize]
    public void ConvertCfg_InvalidConfig_ReturnsErrorString_AndSetsLastError()
    {
        // Intentionally invalid
        const int invalidCfg = 999999;

        var result = _opencc.ConvertCfg("龙马精神", invalidCfg);

        // Contract: should return an allocated error string, not NULL (unless OOM/NULL inputs)
        Assert.IsFalse(string.IsNullOrEmpty(result));

        // Expect format: "Invalid config: <value>"
        // Use Contains instead of strict equals to avoid minor wording differences.
        Assert.Contains("Invalid config", result);
        Assert.Contains(invalidCfg.ToString(), result);

        // Also stored internally and retrievable via opencc_last_error()
        var last = OpenccFmmseg.LastError();
        Assert.Contains("Invalid config", last);
        Assert.Contains(invalidCfg.ToString(), last);
    }

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

    [TestClass]
    public sealed class OpenccNativeConfigTests
    {
        [TestMethod]
        public void NativeConfigNameToId_ConfigNameToIdNative_Works_And_IsCaseInsensitive()
        {
            var ok = OpenccFmmseg.ConfigNameToIdNative("S2TWP", out var config);
            Assert.IsTrue(ok, "ConfigNameToIdNative should succeed for valid canonical name.");
            Assert.AreEqual(OpenccConfig.S2TWP, config);

            // whitespace / invalid
            ok = OpenccFmmseg.ConfigNameToIdNative("   ", out config);
            Assert.IsFalse(ok);

            ok = OpenccFmmseg.ConfigNameToIdNative("not-a-config", out config);
            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void NativeConfigIdToName_ConfigIdToNameNative_Works_And_RoundTrips()
        {
            // id -> name
            var ok = OpenccFmmseg.ConfigIdToNameNative(OpenccConfig.T2HK, out var name);

            Assert.IsTrue(ok, "ConfigIdToNameNative should succeed for valid configId.");
            Assert.IsFalse(string.IsNullOrEmpty(name));
            Assert.AreEqual("t2hk", name, "Native should return canonical lowercase name.");

            // name -> id roundtrip
            ok = OpenccFmmseg.ConfigNameToIdNative(name, out var round);

            Assert.IsTrue(ok, "ConfigNameToIdNative should succeed for name returned by native.");
            Assert.AreEqual(OpenccConfig.T2HK, round);

            // invalid id (cast) should fail
            ok = OpenccFmmseg.ConfigIdToNameNative((OpenccConfig)999999, out name);
            Assert.IsFalse(ok, "ConfigIdToNameNative should fail for invalid enum value.");
            Assert.AreEqual(string.Empty, name);
        }

        [TestMethod]
        public void AbiNoAndVersionStringTest()
        {
            var abiNum = OpenccFmmseg.GetNativeAbiNumber();
            var abiVersion = OpenccFmmseg.GetNativeVersionString();

            Assert.AreEqual(1, abiNum, "AbiNum should be 1.");

            Assert.IsFalse(string.IsNullOrWhiteSpace(abiVersion), "Version string should not be empty.");

            var parts = abiVersion.Split('.');
            Assert.HasCount(3, parts, "Version should have format x.y.z.");

            foreach (var part in parts)
            {
                Assert.IsTrue(
                    int.TryParse(part, out var value) && value >= 0,
                    $"Version component '{part}' must be a non-negative integer."
                );
            }
        }
    }

    [TestClass]
    public class OpenccFmmsegUtf8MemTests
    {
        private const string Input = "春眠不觉晓，处处闻啼鸟";
        private const string Expected = "春眠不覺曉，處處聞啼鳥";

        private static OpenccFmmseg CreateOpencc()
        {
            return new OpenccFmmseg();
        }

        [TestMethod]
        public void ConvertCfgToUtf8Z_S2T_ReturnsUtf8Z_WithExpectedText()
        {
            using var opencc = CreateOpencc();
            const int cfg = (int)OpenccConfig.S2T;

            var utf8Z = opencc.ConvertCfgToUtf8Z(Input, cfg, punctuation: false);

            Assert.IsNotNull(utf8Z);
            Assert.IsGreaterThanOrEqualTo(1, utf8Z.Length, "UTF-8Z output must have at least a trailing NUL byte.");
            Assert.AreEqual(0, utf8Z[^1], "UTF-8Z output must be NUL-terminated.");

            // Decode excluding trailing '\0'
            var s = Encoding.UTF8.GetString(utf8Z, 0, utf8Z.Length - 1);
            Assert.AreEqual(Expected, s, "Converted string mismatch (S2T).");

            // Punctuation should remain the fullwidth comma for S2T.
            Assert.Contains("，", s, "Expected punctuation '，' to remain unchanged for this test case.");
        }

        [TestMethod]
        public void TryConvertCfgToUtf8_S2T_SizeQuery_ExactWrite_AndTooSmallBuffer()
        {
            using var opencc = CreateOpencc();
            const int cfg = (int)OpenccConfig.S2T;

            // 1) Size-query via empty destination: should return false (too small),
            // but must still report requiredBytes.
            var empty = Span<byte>.Empty;
            var okQuery = opencc.TryConvertCfgToUtf8(
                Input,
                cfg,
                punctuation: false,
                destination: empty,
                out var requiredBytes);

            Assert.IsFalse(okQuery, "Empty destination should fail (too small), but still provide requiredBytes.");
            Assert.IsGreaterThan(0, requiredBytes, "requiredBytes must be > 0.");
            Assert.IsGreaterThanOrEqualTo(1, requiredBytes, "requiredBytes must include trailing NUL.");

            // 2) Too-small buffer (requiredBytes - 1) should fail and keep requiredBytes stable
            if (requiredBytes > 1)
            {
                var tooSmall = new byte[requiredBytes - 1];
                var okTooSmall = opencc.TryConvertCfgToUtf8(
                    Input,
                    cfg,
                    punctuation: false,
                    destination: tooSmall,
                    out var requiredBytes2);

                Assert.IsFalse(okTooSmall, "Buffer smaller than required must fail.");
                Assert.AreEqual(requiredBytes, requiredBytes2,
                    "requiredBytes should remain consistent across calls.");
            }

            // 3) Exact-sized buffer should succeed
            var dst = new byte[requiredBytes];
            var okWrite = opencc.TryConvertCfgToUtf8(
                Input,
                cfg,
                punctuation: false,
                destination: dst,
                out var requiredBytes3);

            Assert.IsTrue(okWrite, "Exact-sized buffer should succeed.");
            Assert.AreEqual(requiredBytes, requiredBytes3,
                "requiredBytes should match the exact buffer size used.");

            // Must be NUL-terminated
            Assert.AreEqual(0, dst[^1], "Destination must end with NUL byte.");

            // Decode excluding trailing '\0'
            var converted = Encoding.UTF8.GetString(dst, 0, dst.Length - 1);
            Assert.AreEqual(Expected, converted, "Converted string mismatch (S2T).");

            // Sanity: should differ from input due to 覺曉 conversion
            Assert.AreNotEqual(Input, converted, "S2T conversion should modify the string for this input.");
            Assert.Contains("覺曉", converted, "Expected substring '覺曉' not found in conversion output.");
        }

        [TestMethod]
        public void TryConvertCfgToUtf8_EmptyInput_WritesSingleNul()
        {
            using var opencc = CreateOpencc();
            var cfg = (int)OpenccConfig.S2T;

            var dst = new byte[1];
            var ok = opencc.TryConvertCfgToUtf8(
                input: "",
                configId: cfg,
                punctuation: false,
                destination: dst,
                out var requiredBytes);

            Assert.IsTrue(ok, "Empty input should succeed when destination has at least 1 byte.");
            Assert.AreEqual(1, requiredBytes, "Empty input requires exactly 1 byte (NUL).");
            Assert.AreEqual(0, dst[0], "Empty input output must be a single NUL byte.");
        }

        [TestMethod]
        [DoNotParallelize]
        public void InvalidConfigId_ShouldFail_AndSetLastError()
        {
            using var opencc = CreateOpencc();
            const int invalidCfg = 9999;

            // --- A) ConvertCfgToUtf8Z: wrapper throws on invalid config

            InvalidOperationException? ex = null;

            try
            {
                _ = opencc.ConvertCfgToUtf8Z(Input, invalidCfg, punctuation: false);
                Assert.Fail("Expected InvalidOperationException for invalid configId.");
            }
            catch (InvalidOperationException e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex);
            Assert.Contains("invalid config", ex.Message.ToLowerInvariant());
            Assert.Contains(invalidCfg.ToString(), ex.Message);

            // NOTE:
            // OpenccFmmseg.LastError() is backed by a static/native global error slot.
            // In parallel MSTest execution, another test invoking OpenCC APIs may
            // overwrite or clear the last error before we read it here.
            // If test execution is parallelized, this assertion may intermittently
            // observe "no_error" even though this test triggered the invalid config.
            //
            // If such flakiness appears, consider:
            //   - Disabling parallelization for this test class, or
            //   - Serializing tests that depend on LastError(), or
            //   - Refactoring native LastError to be thread-local instead of global.
            var err = OpenccFmmseg.LastError();
            Assert.IsFalse(string.IsNullOrWhiteSpace(err), "LastError should be set after invalid config failure.");
            Assert.Contains("invalid config", err.ToLowerInvariant());
            Assert.Contains(invalidCfg.ToString(), err);

            // --- B) TryConvertCfgToUtf8: should report required bytes and fail
            var empty = Span<byte>.Empty;
            var okQuery = opencc.TryConvertCfgToUtf8(
                Input,
                invalidCfg,
                punctuation: false,
                destination: empty,
                out var requiredBytes);

            Assert.IsFalse(okQuery, "Invalid config should fail TryConvertCfgToUtf8.");
            Assert.IsGreaterThanOrEqualTo(0, requiredBytes,
                "requiredBytes should be set (may be 0 depending on native behavior).");

            var err2 = OpenccFmmseg.LastError();
            Assert.IsFalse(string.IsNullOrWhiteSpace(err2));
            Assert.Contains("invalid config", err2.ToLowerInvariant());
            Assert.Contains(invalidCfg.ToString(), err2);
        }
    }
}