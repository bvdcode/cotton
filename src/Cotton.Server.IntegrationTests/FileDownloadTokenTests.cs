// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Helpers;
using Microsoft.AspNetCore.WebUtilities;

namespace Cotton.Server.IntegrationTests
{
    public class FileDownloadTokenTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void Token_IsSelfContainedAcrossInstances_AndBoundToMasterKey()
        {
            CottonEncryptionSettings settings = CreateSettings();
            using FileDownloadTokenService issuer = CreateService(settings);
            using FileDownloadTokenService restarted = CreateService(settings);
            using FileDownloadTokenService other = CreateService(CreateSettings());
            Guid userId = Guid.NewGuid();
            Guid fileId = Guid.NewGuid();
            string token = issuer.Create(userId, fileId, TimeSpan.FromHours(24));
            string second = issuer.Create(userId, fileId, TimeSpan.FromHours(24));
            Assert.Multiple(() =>
            {
                Assert.That(second, Is.Not.EqualTo(token));
                Assert.That(restarted.TryRead(token, out FileDownloadToken? payload), Is.True);
                Assert.That(payload, Is.EqualTo(new FileDownloadToken(userId, fileId, Now.AddHours(24))));
                Assert.That(other.TryRead(token, out _), Is.False);
            });
        }

        [Test]
        public void Token_ExpiresAtTheDeadline_AndRejectsInvalidLifetimes()
        {
            AdjustableTimeProvider clock = new(Now);
            using FileDownloadTokenService service = CreateService(CreateSettings(), clock);
            string token = service.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(1));
            clock.Advance(TimeSpan.FromSeconds(59));
            Assert.That(service.TryRead(token, out _), Is.True);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.That(service.TryRead(token, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => service.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero));
        }

        [Test]
        public void Token_RejectsTamperingMalformedInputAndOtherPurposes()
        {
            CottonEncryptionSettings settings = CreateSettings();
            using FileDownloadTokenService service = CreateService(settings);
            using DatabaseBackupTokenService backups = new(settings, NullLogger<DatabaseBackupTokenService>.Instance);
            string token = service.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(1));
            byte[] bytes = WebEncoders.Base64UrlDecode(token[5..]);
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] ^= 1;
                string tampered = "ctd1." + WebEncoders.Base64UrlEncode(bytes);
                Assert.That(service.TryRead(tampered, out _), Is.False, $"position {index}");
                bytes[index] ^= 1;
            }
            foreach (string invalid in new[] { "", "ctd2." + token[5..], token[..^1], token + "A", "ctd1." + new string('!', token.Length - 5), backups.Create() })
            {
                Assert.That(service.TryRead(invalid, out _), Is.False);
            }
            Assert.That(backups.Validate(token), Is.False);
        }

        private static CottonEncryptionSettings CreateSettings() =>
            new() { MasterEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };

        private static FileDownloadTokenService CreateService(CottonEncryptionSettings settings, TimeProvider? clock = null) =>
            new(settings, clock ?? new AdjustableTimeProvider(Now), NullLogger<FileDownloadTokenService>.Instance);
    }
}
