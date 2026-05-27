// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Cotton.Server.Services.KeyManagement;
using EasyExtensions.Models.Enums;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class KeyringDatabaseIntegrityTests
{
    [Test]
    public void KeyringProvider_SignsWithRandomPrimary_AndVerifiesLegacyMacs()
    {
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringPlainState state = KeyringV1UpgradeBuilder.CreateInitialState(
            settings,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            keyringId: "test-keyring",
            createdAtUtc: DateTimeOffset.Parse("2026-05-27T00:00:00Z"));
        var legacyProtector = new DatabaseIntegrityProtector(new DatabaseIntegrityKeyProvider(settings));
        var keyringProtector = new DatabaseIntegrityProtector(new KeyringDatabaseIntegrityKeyProvider(state));
        var descriptor = new UserIntegrityDescriptor();
        User user = CreateUser();

        byte[] legacyMac = legacyProtector.Sign(user, descriptor);
        byte[] keyringMac = keyringProtector.Sign(user, descriptor);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(keyringMac, Is.Not.EqualTo(legacyMac));
            Assert.That(keyringProtector.Verify(user, descriptor, keyringMac), Is.True);
            Assert.That(keyringProtector.Verify(user, descriptor, legacyMac), Is.True);
        }

        user.Role = UserRole.Admin;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(keyringProtector.Verify(user, descriptor, keyringMac), Is.False);
            Assert.That(keyringProtector.Verify(user, descriptor, legacyMac), Is.False);
        }
    }

    private static User CreateUser()
    {
        return new User
        {
            Username = "alice",
            PasswordPhc = "password",
            WebDavTokenPhc = "webdav",
            Role = UserRole.User,
            Email = "alice@example.test",
            IsEmailVerified = true,
            IsTotpEnabled = true,
            TotpSecretEncrypted = [1, 2, 3, 4],
            TotpEnabledAt = DateTime.Parse("2026-05-27T00:00:00Z").ToUniversalTime()
        };
    }
}
