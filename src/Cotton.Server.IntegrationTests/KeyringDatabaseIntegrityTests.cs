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

    [Test]
    public void KeyringAwareProvider_SwitchesFromLegacy_WhenRuntimeKeyringIsLoaded()
    {
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringPlainState state = KeyringV1UpgradeBuilder.CreateInitialState(
            settings,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            keyringId: "test-keyring",
            createdAtUtc: DateTimeOffset.Parse("2026-05-27T00:00:00Z"));
        var runtimeState = new KeyringRuntimeState();
        var keyProvider = new KeyringAwareDatabaseIntegrityKeyProvider(settings, runtimeState);
        var protector = new DatabaseIntegrityProtector(keyProvider);
        var descriptor = new UserIntegrityDescriptor();
        User user = CreateUser();

        byte[] legacyMac = protector.Sign(user, descriptor);
        runtimeState.Set(CreateBootstrapResult(state));
        byte[] keyringMac = protector.Sign(user, descriptor);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(keyringMac, Is.Not.EqualTo(legacyMac));
            Assert.That(protector.Verify(user, descriptor, keyringMac), Is.True);
            Assert.That(protector.Verify(user, descriptor, legacyMac), Is.True);
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

    private static KeyringBootstrapResult CreateBootstrapResult(KeyringPlainState state)
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-05-27T00:00:00Z");
        var access = new KeyringAccessEnvelope(
            KeyringFormat.AccessEnvelopeMagic,
            state.InstanceId,
            state.KeyringId,
            state.RootEpoch,
            Generation: 1,
            ParentHash: null,
            CreatedAtUtc: now,
            Recipients: []);
        var statePointer = new KeyringObjectPointer(
            KeyringObjectKind.StateSnapshot,
            state.StateGeneration,
            Hash: new string('0', 64),
            ObjectName: "state",
            CommittedAtUtc: now);
        var accessPointer = new KeyringObjectPointer(
            KeyringObjectKind.AccessEnvelope,
            Generation: 1,
            Hash: new string('0', 64),
            ObjectName: "access",
            CommittedAtUtc: now);

        return new KeyringBootstrapResult(
            Created: false,
            State: state,
            AccessEnvelope: access,
            StatePointer: statePointer,
            AccessPointer: accessPointer,
            UnlockedSlotId: null);
    }
}
