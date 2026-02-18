// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using EasyExtensions.Crypto;
using Microsoft.Extensions.Configuration;

namespace Cotton.Autoconfig.Tests
{
    public class ConfigurationBuilderExtensionsTests
    {
        private const string EnvVar = "COTTON_MASTER_KEY";
        private string? _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = Environment.GetEnvironmentVariable(EnvVar);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(EnvVar, _saved);
        }

        [Test]
        public void MasterKeyLength_Is_32()
        {
            Assert.That(ConfigurationBuilderExtensions.DefaultKeyLength, Is.EqualTo(32));
        }

        [Test]
        public void AddCottonOptions_Throws_When_MasterKey_Length_Is_Not_32()
        {
            Environment.SetEnvironmentVariable(EnvVar, "too-short");
            var builder = new ConfigurationBuilder();
            var ex = Assert.Throws<InvalidOperationException>(() => builder.AddCottonOptions());
            Assert.That(ex!.Message, Does.Contain(ConfigurationBuilderExtensions.DefaultKeyLength.ToString()));
        }

        [Test]
        public void AddCottonOptions_Derives_Pepper_And_MasterKey_From_Root()
        {
            // Arrange fixed 32-char master
            const string root = "0123456789abcdef0123456789abcdef"; // 32 chars
            Environment.SetEnvironmentVariable(EnvVar, root);

            string expectedPepper = KeyDerivation.DeriveSubkeyBase64(root, "CottonPepper", ConfigurationBuilderExtensions.DefaultKeyLength);
            string expectedMaster = KeyDerivation.DeriveSubkeyBase64(root, "CottonMasterEncryptionKey", ConfigurationBuilderExtensions.DefaultKeyLength);

            // Act
            var cfg = new ConfigurationBuilder().AddCottonOptions().Build();

            using (Assert.EnterMultipleScope())
            {
                // Assert exact values
                Assert.That(cfg[nameof(CottonEncryptionSettings.Pepper)], Is.EqualTo(expectedPepper));
                Assert.That(cfg[nameof(CottonEncryptionSettings.MasterEncryptionKey)], Is.EqualTo(expectedMaster));
                Assert.That(cfg[nameof(CottonEncryptionSettings.MasterEncryptionKeyId)], Is.EqualTo("1"));
            }

            // And base64 decodes to required length
            var pepperBytes = Convert.FromBase64String(cfg[nameof(CottonEncryptionSettings.Pepper)]!);
            var masterBytes = Convert.FromBase64String(cfg[nameof(CottonEncryptionSettings.MasterEncryptionKey)]!);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(pepperBytes, Has.Length.EqualTo(ConfigurationBuilderExtensions.DefaultKeyLength));
                Assert.That(masterBytes, Has.Length.EqualTo(ConfigurationBuilderExtensions.DefaultKeyLength));
            }
        }

        [Test]
        public void AddCottonOptions_Different_Root_Produces_Different_Derivatives()
        {
            const string rootA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"; // 32 chars
            const string rootB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"; // 32 chars

            Environment.SetEnvironmentVariable(EnvVar, rootA);
            var cfgA = new ConfigurationBuilder().AddCottonOptions().Build();
            string pepperA = cfgA[nameof(CottonEncryptionSettings.Pepper)]!;
            string masterA = cfgA[nameof(CottonEncryptionSettings.MasterEncryptionKey)]!;

            Environment.SetEnvironmentVariable(EnvVar, rootB);
            var cfgB = new ConfigurationBuilder().AddCottonOptions().Build();
            string pepperB = cfgB[nameof(CottonEncryptionSettings.Pepper)]!;
            string masterB = cfgB[nameof(CottonEncryptionSettings.MasterEncryptionKey)]!;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(pepperA, Is.Not.EqualTo(pepperB));
                Assert.That(masterA, Is.Not.EqualTo(masterB));
            }
        }
    }
}
