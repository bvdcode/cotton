// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public class DatabaseBackupTokenTests
    {
        [Test]
        public void Token_SurvivesRestart_AndIsBoundToMasterKey()
        {
            CottonEncryptionSettings settings = new() { MasterEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
            using DatabaseBackupTokenService issuer = CreateService(settings);
            using DatabaseBackupTokenService restarted = CreateService(settings);
            using DatabaseBackupTokenService other = CreateService(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            });
            string first = issuer.Create();
            string second = issuer.Create();
            Assert.Multiple(() =>
            {
                Assert.That(second, Is.Not.EqualTo(first));
                Assert.That(restarted.Validate(first), Is.True);
                Assert.That(restarted.Validate(second), Is.True);
                Assert.That(other.Validate(first), Is.False);
            });
        }

        [Test]
        public void Token_RejectsTamperingAndMalformedInput()
        {
            using DatabaseBackupTokenService service = CreateService(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            });
            string token = service.Create();
            string tampered = token[..10] + (token[10] == 'A' ? 'B' : 'A') + token[11..];
            foreach (string invalid in new[] { "", "Bearer " + token, token[..^1], token + "A", tampered, "ctb1." + new string('!', token.Length - 5) })
            {
                Assert.That(service.Validate(invalid), Is.False);
            }
        }

        private static DatabaseBackupTokenService CreateService(CottonEncryptionSettings settings) =>
            new(settings, NullLogger<DatabaseBackupTokenService>.Instance);
    }
}
