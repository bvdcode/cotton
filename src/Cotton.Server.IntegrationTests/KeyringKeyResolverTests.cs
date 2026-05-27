// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Services.KeyManagement;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class KeyringKeyResolverTests
{
    [Test]
    public void GetPrimary_ReturnsRandomV2ChunkKey()
    {
        KeyringPlainState state = CreateState();
        var resolver = new KeyringPlainStateKeyResolver(state);

        KeyringResolvedKey key = resolver.GetPrimary(KeyringKeyPurpose.ChunkAead);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(key.Id, Is.EqualTo(KeyringV1UpgradeBuilder.FirstV2ChunkKeyId));
            Assert.That(key.Status, Is.EqualTo(KeyringKeyStatus.EncryptDecrypt));
            Assert.That(key.Origin, Is.EqualTo(KeyringKeyOrigin.RandomV2));
            Assert.That(key.Material, Has.Length.EqualTo(KeyringFormat.KeySizeBytes));
        }
    }

    [Test]
    public void GetById_ReturnsLegacyChunkKey_AsDecryptOnly()
    {
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringPlainState state = CreateState(settings);
        var resolver = new KeyringPlainStateKeyResolver(state);

        KeyringResolvedKey key = resolver.GetById(KeyringKeyPurpose.ChunkAead, KeyringV1UpgradeBuilder.LegacyKeyId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(key.Status, Is.EqualTo(KeyringKeyStatus.DecryptOnly));
            Assert.That(key.Origin, Is.EqualTo(KeyringKeyOrigin.LegacyV1MasterDerived));
            Assert.That(Convert.ToBase64String(key.Material), Is.EqualTo(settings.MasterEncryptionKey));
        }
    }

    [Test]
    public void GetById_FailsClosed_ForUnknownKeyId()
    {
        var resolver = new KeyringPlainStateKeyResolver(CreateState());

        Assert.That(
            () => resolver.GetById(KeyringKeyPurpose.ChunkAead, 999),
            Throws.TypeOf<KeyNotFoundException>());
    }

    [Test]
    public void GetPrimary_Fails_WhenPrimaryKeyIsNotEncryptEnabled()
    {
        KeyringPlainState state = CreateState();
        KeyringPlainState broken = state with
        {
            Primary = state.Primary with
            {
                ChunkAead = KeyringV1UpgradeBuilder.LegacyKeyId
            }
        };
        var resolver = new KeyringPlainStateKeyResolver(broken);

        Assert.That(
            () => resolver.GetPrimary(KeyringKeyPurpose.ChunkAead),
            Throws.TypeOf<InvalidOperationException>());
    }

    private static KeyringPlainState CreateState(CottonEncryptionSettings? settings = null)
    {
        settings ??= ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        return KeyringV1UpgradeBuilder.CreateInitialState(
            settings,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            keyringId: "test-keyring",
            createdAtUtc: DateTimeOffset.Parse("2026-05-27T00:00:00Z"));
    }
}
