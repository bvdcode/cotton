// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Crypto.Tests
{
    public class KeyDerivationTests
    {
        private const string CompatibilityRootKey = "0123456789abcdef0123456789abcdef";
        private const string Master1 = "master-key-1";
        private const string Master2 = "master-key-2";
        private const string PurposeA = "encryption-key";
        private const string PurposeB = "hmac-key";

        [Test]
        public void DeriveSubkey_MatchesRfc5869Sha256TestCaseOne()
        {
            byte[] inputKeyMaterial = Convert.FromHexString(
                "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b");
            byte[] salt = Convert.FromHexString("000102030405060708090a0b0c");
            byte[] info = Convert.FromHexString("f0f1f2f3f4f5f6f7f8f9");
            byte[] expected = Convert.FromHexString(
                "3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865");

            byte[] derived = KeyDerivation.DeriveSubkey(inputKeyMaterial, info, expected.Length, salt);

            Assert.That(derived, Is.EqualTo(expected));
        }

        [TestCase("CottonPepper", "93KxynoTsD7Dur8jvNDVXs3K+rG9JipcGc9RjWTGws0=")]
        [TestCase("CottonMasterEncryptionKey", "cn9Iuuhpj8AlOj0AioqiFXj18LdOg0QmuV/UoI8auro=")]
        public void DeriveSubkeyBase64_PreservesCottonCompatibilityVectors(
            string purpose,
            string expected)
        {
            string derived = KeyDerivation.DeriveSubkeyBase64(
                CompatibilityRootKey,
                purpose,
                32);

            Assert.That(derived, Is.EqualTo(expected));
        }

        [Test]
        public void DeriveSubkey_PreservesDatabaseIntegrityCompatibilityVector()
        {
            byte[] masterKey = Convert.FromHexString(
                "727f48bae8698fc0253a3d008a8aa21578f5f0b74e834426b95fd4a08f1ababa");
            byte[] purpose = System.Text.Encoding.UTF8.GetBytes("CottonDbIntegrityKey:v1");
            byte[] expected = Convert.FromHexString(
                "6660e613a02857a2665d42dc6275e47bf2fb2f09d0dd03d39d3fea7dccd10d3f");

            byte[] derived = KeyDerivation.DeriveSubkey(masterKey, purpose, expected.Length);

            Assert.That(derived, Is.EqualTo(expected));
        }

        [Test]
        public void DeriveSubkey_Returns_Requested_Length([Values(0, 1, 16, 32, 48, 64, 100)] int len)
        {
            var bytes = KeyDerivation.DeriveSubkey(Master1, PurposeA, len);
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes, Has.Length.EqualTo(len));
            if (len > 0)
            {
                // Not all zeros
                Assert.That(bytes.Any(b => b != 0), Is.True);
            }
        }

        [Test]
        public void Deterministic_For_Same_Inputs()
        {
            var a1 = KeyDerivation.DeriveSubkey(Master1, PurposeA, 48);
            var a2 = KeyDerivation.DeriveSubkey(Master1, PurposeA, 48);
            Assert.That(a1, Is.EqualTo(a2));
        }

        [Test]
        public void Different_Master_Produces_Different_Key()
        {
            var a = KeyDerivation.DeriveSubkey(Master1, PurposeA, 48);
            var b = KeyDerivation.DeriveSubkey(Master2, PurposeA, 48);
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void Different_Purpose_Produces_Different_Key()
        {
            var a = KeyDerivation.DeriveSubkey(Master1, PurposeA, 48);
            var b = KeyDerivation.DeriveSubkey(Master1, PurposeB, 48);
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void Base64_Matches_Raw_Encoding()
        {
            var bytes = KeyDerivation.DeriveSubkey(Master1, PurposeA, 32);
            var b64A = Cotton.Crypto.KeyDerivation.DeriveSubkeyBase64(Master1, PurposeA, 32);
            var b64B = Convert.ToBase64String(bytes);
            Assert.That(b64A, Is.EqualTo(b64B));
        }

        [Test]
        public void Length32_Vs_Length64_Prefixes_NotDiffer_By_Design()
        {
            // length 32 uses HMAC(purpose) directly
            var l32 = KeyDerivation.DeriveSubkey(Master1, PurposeA, 32);
            // length 64 uses HMAC(purpose||1) + HMAC(purpose||2)
            var l64 = KeyDerivation.DeriveSubkey(Master1, PurposeA, 64);
            Assert.That(l32, Is.EqualTo(l64.Take(32).ToArray()));
        }

        [Test]
        public void Longer_Length_Extends_With_Deterministic_Blocks()
        {
            // For lengths > 32, result is concatenation of counter-based blocks starting at 1,
            // so prefix of 64 should match prefix of 96.
            var l64 = KeyDerivation.DeriveSubkey(Master1, PurposeA, 64);
            var l96 = KeyDerivation.DeriveSubkey(Master1, PurposeA, 96);
            Assert.That(l96.Take(64).ToArray(), Is.EqualTo(l64));
        }
    }
}
