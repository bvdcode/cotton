// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Services.KeyManagement;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class KeyringRotationServiceTests
{
    [Test]
    public async Task RotateLegacyMasterUnlock_RewrapsStateForNewUnlock_AndRejectsOldUnlock()
    {
        string root = CreateTempDirectory();
        var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
        var bootstrap = new KeyringBootstrapService(store);
        var rotation = new KeyringRotationService(store);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        KeyringBootstrapResult initial = await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringRotationResult rotated = await rotation.RotateLegacyMasterUnlockAsync(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        KeyringBootstrapResult reopened = await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rotated.State.RootEpoch, Is.EqualTo(initial.State.RootEpoch + 1));
            Assert.That(rotated.State.StateGeneration, Is.EqualTo(initial.State.StateGeneration + 1));
            Assert.That(rotated.AccessEnvelope.Generation, Is.EqualTo(initial.AccessEnvelope.Generation + 1));
            Assert.That(rotated.State.ParentStateHash, Is.EqualTo(initial.StatePointer.Hash));
            Assert.That(rotated.AccessEnvelope.ParentHash, Is.EqualTo(initial.AccessPointer.Hash));
            Assert.That(reopened.Created, Is.False);
            Assert.That(reopened.State.RootEpoch, Is.EqualTo(rotated.State.RootEpoch));
            Assert.That(reopened.State.KeyringId, Is.EqualTo(initial.State.KeyringId));
            Assert.That(reopened.State.Primary.ChunkAead, Is.EqualTo(initial.State.Primary.ChunkAead));
            Assert.That(
                async () => await bootstrap.OpenOrCreateFromV1Async(
                    settings,
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                Throws.InstanceOf<UnauthorizedAccessException>());
        }
    }

    [Test]
    public async Task RotateLegacyMasterUnlock_RejectsWrongCurrentUnlock()
    {
        string root = CreateTempDirectory();
        var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
        var bootstrap = new KeyringBootstrapService(store);
        var rotation = new KeyringRotationService(store);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.That(
            async () => await rotation.RotateLegacyMasterUnlockAsync(
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "cccccccccccccccccccccccccccccccc"),
            Throws.InstanceOf<UnauthorizedAccessException>());
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "keyring-rotation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
