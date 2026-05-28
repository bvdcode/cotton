// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Models.Dto;
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

    [Test]
    [NonParallelizable]
    public async Task AdminService_CreatesRecoverySlot_AndKeepsMasterUnlockWorking()
    {
        string root = CreateTempDirectory();
        string? originalEnabled = Environment.GetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable);
        string? originalPath = Environment.GetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable);
        string unlock = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string recoverySecret = new string('b', 64);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(unlock);

        try
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, "1");
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, root);
            var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
            var bootstrap = new KeyringBootstrapService(store);
            KeyringBootstrapResult initial = await bootstrap.OpenOrCreateFromV1Async(settings, unlock);
            var runtimeState = new KeyringRuntimeState();
            runtimeState.Set(initial);
            var admin = new KeyringAdminService(store, runtimeState);

            KeyringCreateRecoverySlotResponseDto result = await admin.CreateRecoverySlotAsync(
                unlock,
                recoverySecret);
            KeyringBootstrapResult openedWithMaster = await bootstrap.OpenOrCreateFromV1Async(settings, unlock);
            KeyringBootstrapResult openedWithRecovery = await bootstrap.OpenOrCreateFromV1Async(settings, recoverySecret);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.SlotId, Does.StartWith("recovery:"));
                Assert.That(result.RootEpoch, Is.EqualTo(initial.State.RootEpoch));
                Assert.That(result.AccessGeneration, Is.EqualTo(initial.AccessEnvelope.Generation));
                Assert.That(result.StateGeneration, Is.EqualTo(initial.State.StateGeneration));
                Assert.That(result.RecoveryKit.AccessGeneration, Is.EqualTo(result.AccessGeneration));
                Assert.That(openedWithMaster.State.KeyringId, Is.EqualTo(initial.State.KeyringId));
                Assert.That(openedWithRecovery.State.KeyringId, Is.EqualTo(initial.State.KeyringId));
                Assert.That(openedWithRecovery.UnlockedSlotId, Is.EqualTo(result.SlotId));
                Assert.That(openedWithRecovery.AccessEnvelope.Recipients, Has.Count.EqualTo(2));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, originalEnabled);
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, originalPath);
        }
    }

    [Test]
    [NonParallelizable]
    public async Task AdminService_ExportsRecoveryKit_WithLatestEncryptedObjects()
    {
        string root = CreateTempDirectory();
        string? originalEnabled = Environment.GetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable);
        string? originalPath = Environment.GetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable);
        string unlock = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(unlock);

        try
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, "1");
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, root);
            var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
            var bootstrap = new KeyringBootstrapService(store);
            KeyringBootstrapResult initial = await bootstrap.OpenOrCreateFromV1Async(settings, unlock);
            var runtimeState = new KeyringRuntimeState();
            runtimeState.Set(initial);
            var admin = new KeyringAdminService(store, runtimeState);

            KeyringRecoveryKitDto kit = await admin.ExportRecoveryKitAsync();
            KeyringLoadedObject? accessObject = await store.FindLatestValidAsync(KeyringObjectKind.AccessEnvelope);
            KeyringLoadedObject? stateObject = await store.FindLatestValidAsync(KeyringObjectKind.StateSnapshot);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(kit.Magic, Is.EqualTo("cotton.recovery-kit.v2"));
                Assert.That(kit.Schema, Is.EqualTo(KeyringFormat.SchemaVersion));
                Assert.That(kit.InstanceId, Is.EqualTo(initial.State.InstanceId));
                Assert.That(kit.KeyringId, Is.EqualTo(initial.State.KeyringId));
                Assert.That(kit.RootEpoch, Is.EqualTo(initial.State.RootEpoch));
                Assert.That(kit.AccessGeneration, Is.EqualTo(initial.AccessEnvelope.Generation));
                Assert.That(kit.StateGeneration, Is.EqualTo(initial.State.StateGeneration));
                Assert.That(kit.AccessEnvelopeObjectName, Is.EqualTo(initial.AccessPointer.ObjectName));
                Assert.That(kit.AccessEnvelopeHash, Is.EqualTo(initial.AccessPointer.Hash));
                Assert.That(kit.StateSnapshotObjectName, Is.EqualTo(initial.StatePointer.ObjectName));
                Assert.That(kit.StateSnapshotHash, Is.EqualTo(initial.StatePointer.Hash));
                Assert.That(Convert.FromBase64String(kit.AccessEnvelopeBase64), Is.EqualTo(accessObject!.Bytes));
                Assert.That(Convert.FromBase64String(kit.StateSnapshotBase64), Is.EqualTo(stateObject!.Bytes));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, originalEnabled);
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, originalPath);
        }
    }

    [Test]
    [NonParallelizable]
    public async Task AdminService_ImportsRecoveryKit_AndRestoresMissingReplicaObjects()
    {
        string primaryRoot = CreateTempDirectory();
        string secondaryRoot = CreateTempDirectory();
        string? originalEnabled = Environment.GetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable);
        string? originalPath = Environment.GetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable);
        string unlock = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(unlock);

        try
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, "1");
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, primaryRoot);
            var primaryReplica = new KeyringLocalFileReplica(primaryRoot, "primary");
            var secondaryReplica = new KeyringLocalFileReplica(secondaryRoot, "secondary");
            var primaryStore = new KeyringJournaledObjectStore([primaryReplica]);
            var bootstrap = new KeyringBootstrapService(primaryStore);
            KeyringBootstrapResult initial = await bootstrap.OpenOrCreateFromV1Async(settings, unlock);
            var runtimeState = new KeyringRuntimeState();
            runtimeState.Set(initial);
            var repairStore = new KeyringJournaledObjectStore([primaryReplica, secondaryReplica]);
            var admin = new KeyringAdminService(repairStore, runtimeState);
            KeyringRecoveryKitDto kit = await admin.ExportRecoveryKitAsync();

            KeyringRecoveryKitImportResponseDto result = await admin.ImportRecoveryKitAsync(kit);
            List<string> secondaryNames = await secondaryReplica.ListNamesAsync().ToListAsync();
            KeyringLoadedObject? secondaryState = await new KeyringJournaledObjectStore([secondaryReplica])
                .FindLatestValidAsync(KeyringObjectKind.StateSnapshot);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.RootEpoch, Is.EqualTo(initial.State.RootEpoch));
                Assert.That(result.AccessGeneration, Is.EqualTo(initial.AccessEnvelope.Generation));
                Assert.That(result.StateGeneration, Is.EqualTo(initial.State.StateGeneration));
                Assert.That(result.AccessEnvelopeHash, Is.EqualTo(initial.AccessPointer.Hash));
                Assert.That(result.StateSnapshotHash, Is.EqualTo(initial.StatePointer.Hash));
                Assert.That(secondaryNames, Does.Contain(initial.AccessPointer.ObjectName));
                Assert.That(secondaryNames, Does.Contain(initial.StatePointer.ObjectName));
                Assert.That(secondaryNames, Does.Contain(KeyringObjectNames.GetLatestName(KeyringObjectKind.AccessEnvelope)));
                Assert.That(secondaryNames, Does.Contain(KeyringObjectNames.GetLatestName(KeyringObjectKind.StateSnapshot)));
                Assert.That(secondaryState, Is.Not.Null);
                Assert.That(secondaryState!.Pointer.Hash, Is.EqualTo(initial.StatePointer.Hash));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, originalEnabled);
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, originalPath);
        }
    }

    [Test]
    [NonParallelizable]
    public async Task AdminService_RejectsRecoveryKit_WhenPayloadHashIsTampered()
    {
        string root = CreateTempDirectory();
        string? originalEnabled = Environment.GetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable);
        string? originalPath = Environment.GetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable);
        string unlock = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(unlock);

        try
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, "1");
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, root);
            var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
            var bootstrap = new KeyringBootstrapService(store);
            KeyringBootstrapResult initial = await bootstrap.OpenOrCreateFromV1Async(settings, unlock);
            var runtimeState = new KeyringRuntimeState();
            runtimeState.Set(initial);
            var admin = new KeyringAdminService(store, runtimeState);
            KeyringRecoveryKitDto kit = await admin.ExportRecoveryKitAsync();
            KeyringRecoveryKitDto tampered = CopyKit(kit, stateSnapshotHash: new string('0', 64));

            Assert.That(
                async () => await admin.ImportRecoveryKitAsync(tampered),
                Throws.InstanceOf<InvalidOperationException>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, originalEnabled);
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, originalPath);
        }
    }

    [Test]
    [NonParallelizable]
    public async Task AdminService_RotatesUnlock_AndUpdatesRuntimeState()
    {
        string root = CreateTempDirectory();
        string? originalEnabled = Environment.GetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable);
        string? originalPath = Environment.GetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable);
        string oldUnlock = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string newUnlock = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(oldUnlock);

        try
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, "1");
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, root);
            var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
            var bootstrap = new KeyringBootstrapService(store);
            KeyringBootstrapResult initial = await bootstrap.OpenOrCreateFromV1Async(settings, oldUnlock);
            var runtimeState = new KeyringRuntimeState();
            runtimeState.Set(initial);
            var admin = new KeyringAdminService(store, runtimeState);

            KeyringAdminRotationResult result = await admin.RotateUnlockAsync(oldUnlock, newUnlock);
            KeyringBootstrapResult? current = runtimeState.Current;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.RootEpoch, Is.EqualTo(initial.State.RootEpoch + 1));
                Assert.That(result.AccessGeneration, Is.EqualTo(initial.AccessEnvelope.Generation + 1));
                Assert.That(result.StateGeneration, Is.EqualTo(initial.State.StateGeneration + 1));
                Assert.That(current, Is.Not.Null);
                Assert.That(current!.State.RootEpoch, Is.EqualTo(result.RootEpoch));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, originalEnabled);
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, originalPath);
        }
    }

    private static KeyringRecoveryKitDto CopyKit(
        KeyringRecoveryKitDto kit,
        string? accessEnvelopeHash = null,
        string? stateSnapshotHash = null)
    {
        return new KeyringRecoveryKitDto
        {
            Magic = kit.Magic,
            Schema = kit.Schema,
            ExportedAtUtc = kit.ExportedAtUtc,
            InstanceId = kit.InstanceId,
            KeyringId = kit.KeyringId,
            RootEpoch = kit.RootEpoch,
            AccessGeneration = kit.AccessGeneration,
            StateGeneration = kit.StateGeneration,
            AccessEnvelopeObjectName = kit.AccessEnvelopeObjectName,
            AccessEnvelopeHash = accessEnvelopeHash ?? kit.AccessEnvelopeHash,
            AccessEnvelopeBase64 = kit.AccessEnvelopeBase64,
            StateSnapshotObjectName = kit.StateSnapshotObjectName,
            StateSnapshotHash = stateSnapshotHash ?? kit.StateSnapshotHash,
            StateSnapshotBase64 = kit.StateSnapshotBase64,
        };
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "keyring-rotation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
