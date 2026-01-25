using OpenccFmmsegLib;

namespace OpenccFmmsegLibTests
{
    [TestClass]
    public sealed class OpenccConfigExtensionsTests
    {
        [TestMethod]
        public void DefaultConfig_ReturnsS2T()
        {
            Assert.AreEqual(OpenccConfig.S2T, OpenccConfigExtensions.DefaultConfig());
        }

        // --- TryParseConfig ---

        [TestMethod]
        public void TryParseConfig_ParsesValidNames_CaseInsensitive()
        {
            Assert.IsTrue(OpenccConfigExtensions.TryParseConfig("s2t", out var cfg));
            Assert.AreEqual(OpenccConfig.S2T, cfg);

            Assert.IsTrue(OpenccConfigExtensions.TryParseConfig("S2TWP", out cfg));
            Assert.AreEqual(OpenccConfig.S2TWP, cfg);

            Assert.IsTrue(OpenccConfigExtensions.TryParseConfig("t2hk", out cfg));
            Assert.AreEqual(OpenccConfig.T2HK, cfg);

            Assert.IsTrue(OpenccConfigExtensions.TryParseConfig("Tw2Sp", out cfg));
            Assert.AreEqual(OpenccConfig.TW2SP, cfg);

            Assert.IsTrue(OpenccConfigExtensions.TryParseConfig("JP2T", out cfg));
            Assert.AreEqual(OpenccConfig.JP2T, cfg);
        }

        [TestMethod]
        public void TryParseConfig_InvalidInputs_ReturnsFalse_AndSetsDefaultOut()
        {
            Assert.IsFalse(OpenccConfigExtensions.TryParseConfig(null, out var cfg));
            Assert.AreEqual(default, cfg);

            Assert.IsFalse(OpenccConfigExtensions.TryParseConfig(string.Empty, out cfg));
            Assert.AreEqual(default, cfg);

            Assert.IsFalse(OpenccConfigExtensions.TryParseConfig("   ", out cfg)); // strict (no trim)
            Assert.AreEqual(default, cfg);

            Assert.IsFalse(OpenccConfigExtensions.TryParseConfig("not-a-config", out cfg));
            Assert.AreEqual(default, cfg);

            Assert.IsFalse(OpenccConfigExtensions.TryParseConfig("s2twp ", out cfg)); // strict
            Assert.AreEqual(default, cfg);
        }

        // --- TryGetConfigName ---

        [TestMethod]
        public void TryGetConfigName_ReturnsCanonicalLowercase()
        {
            Assert.IsTrue(OpenccConfigExtensions.TryGetConfigName(OpenccConfig.S2T, out var name));
            Assert.AreEqual("s2t", name);

            Assert.IsTrue(OpenccConfigExtensions.TryGetConfigName(OpenccConfig.S2TWP, out name));
            Assert.AreEqual("s2twp", name);

            Assert.IsTrue(OpenccConfigExtensions.TryGetConfigName(OpenccConfig.T2HK, out name));
            Assert.AreEqual("t2hk", name);

            Assert.IsTrue(OpenccConfigExtensions.TryGetConfigName(OpenccConfig.TW2SP, out name));
            Assert.AreEqual("tw2sp", name);

            Assert.IsTrue(OpenccConfigExtensions.TryGetConfigName(OpenccConfig.T2JP, out name));
            Assert.AreEqual("t2jp", name);
        }

        [TestMethod]
        public void TryGetConfigName_InvalidEnum_ReturnsFalse_AndEmptyName()
        {
            Assert.IsFalse(OpenccConfigExtensions.TryGetConfigName((OpenccConfig)999999, out var name));
            Assert.AreEqual(string.Empty, name);
        }

        // --- Parse / IsValidConfig / ToCanonicalName ---

        [TestMethod]
        public void FromStr_ParsesValidNames_CaseInsensitive()
        {
            Assert.AreEqual(OpenccConfig.S2T, OpenccConfigExtensions.Parse("s2t"));
            Assert.AreEqual(OpenccConfig.S2TWP, OpenccConfigExtensions.Parse("S2TWP"));
            Assert.AreEqual(OpenccConfig.T2HK, OpenccConfigExtensions.Parse("t2hk"));
            Assert.AreEqual(OpenccConfig.T2HK, OpenccConfigExtensions.Parse("T2HK"));
            Assert.AreEqual(OpenccConfig.TW2SP, OpenccConfigExtensions.Parse("Tw2Sp"));
            Assert.AreEqual(OpenccConfig.JP2T, OpenccConfigExtensions.Parse("JP2T"));
        }

        [TestMethod]
        public void FromStr_InvalidName_Throws()
        {
            try
            {
                OpenccConfigExtensions.Parse("not-a-config");
                Assert.Fail("Expected ArgumentException for invalid config name.");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        [TestMethod]
        public void FromStr_Null_Throws()
        {
            try
            {
                OpenccConfigExtensions.Parse(null);
                Assert.Fail("Expected ArgumentNullException for null name.");
            }
            catch (ArgumentNullException)
            {
                // expected
            }
        }

        [TestMethod]
        public void IsSupportedConfig_Works_ForValidAndInvalid()
        {
            Assert.IsTrue(OpenccConfigExtensions.IsValidConfig("s2t"));
            Assert.IsTrue(OpenccConfigExtensions.IsValidConfig("S2TWP"));
            Assert.IsTrue(OpenccConfigExtensions.IsValidConfig("t2hk"));
            Assert.IsTrue(OpenccConfigExtensions.IsValidConfig("TW2TP"));

            Assert.IsFalse(OpenccConfigExtensions.IsValidConfig(null));
            Assert.IsFalse(OpenccConfigExtensions.IsValidConfig(string.Empty));
            Assert.IsFalse(OpenccConfigExtensions.IsValidConfig("   "));
            Assert.IsFalse(OpenccConfigExtensions.IsValidConfig("not-a-config"));
            Assert.IsFalse(OpenccConfigExtensions.IsValidConfig("s2twp ")); // strict (no trim)
        }

        [TestMethod]
        public void ToCanonicalName_ReturnsExpectedLowercase()
        {
            Assert.AreEqual("s2t", OpenccConfig.S2T.ToCanonicalName());
            Assert.AreEqual("s2twp", OpenccConfig.S2TWP.ToCanonicalName());
            Assert.AreEqual("t2hk", OpenccConfig.T2HK.ToCanonicalName());
            Assert.AreEqual("tw2sp", OpenccConfig.TW2SP.ToCanonicalName());
            Assert.AreEqual("t2jp", OpenccConfig.T2JP.ToCanonicalName());
        }

        [TestMethod]
        public void CanonicalName_RoundTrip_AllEnumValues_UsingTryHelpers()
        {
            foreach (var cfg in Enum.GetValues<OpenccConfig>())
            {
                Assert.IsTrue(OpenccConfigExtensions.TryGetConfigName(cfg, out var name), "TryGetConfigName failed for: " + cfg);
                Assert.IsFalse(string.IsNullOrEmpty(name), "Empty canonical name for: " + cfg);

                Assert.IsTrue(OpenccConfigExtensions.TryParseConfig(name, out var parsed), "TryParseConfig failed for: " + cfg + " => " + name);
                Assert.AreEqual(cfg, parsed, "Round-trip failed for: " + cfg + " => " + name);
            }
        }

        [TestMethod]
        public void ToCanonicalName_InvalidEnum_Throws()
        {
            try
            {
                const OpenccConfig invalid = (OpenccConfig)999999;
                _ = invalid.ToCanonicalName();
                Assert.Fail("Expected ArgumentOutOfRangeException for invalid enum value.");
            }
            catch (ArgumentOutOfRangeException)
            {
                // expected
            }
        }
    }
}
