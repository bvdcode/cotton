// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Services.KeyManagement;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class KeyringDiagnosticsServiceTests
{
    [Test]
    public async Task GetSnapshotAsync_ReportsMissingObjects()
    {
        var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(CreateTempDirectory())]);
        var diagnostics = new KeyringDiagnosticsService(store);

        KeyringDiagnosticsSnapshot snapshot = await diagnostics.GetSnapshotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.AccessEnvelopePresent, Is.False);
            Assert.That(snapshot.StateSnapshotPresent, Is.False);
            Assert.That(snapshot.Warnings, Does.Contain("keyring-access-missing"));
            Assert.That(snapshot.Warnings, Does.Contain("keyring-state-missing"));
        }
    }

    [Test]
    public async Task GetSnapshotAsync_VerifiesUnlock_AndReportsLegacyDebt()
    {
        string root = CreateTempDirectory();
        var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
        var bootstrap = new KeyringBootstrapService(store);
        var diagnostics = new KeyringDiagnosticsService(store);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringDiagnosticsSnapshot snapshot = await diagnostics.GetSnapshotAsync(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.AccessEnvelopePresent, Is.True);
            Assert.That(snapshot.StateSnapshotPresent, Is.True);
            Assert.That(snapshot.UnlockSucceeded, Is.True);
            Assert.That(snapshot.UnlockedSlotId, Is.EqualTo(KeyringBootstrapService.DefaultLegacyMasterSlotId));
            Assert.That(snapshot.AccessGeneration, Is.EqualTo(1));
            Assert.That(snapshot.StateGeneration, Is.EqualTo(1));
            Assert.That(snapshot.RootEpoch, Is.EqualTo(1));
            Assert.That(snapshot.KeyCount, Is.GreaterThan(0));
            Assert.That(snapshot.RecoverySlotCount, Is.EqualTo(0));
            Assert.That(snapshot.LegacyDecryptOnlyKeyCount, Is.GreaterThan(0));
            Assert.That(snapshot.Warnings, Does.Contain("keyring-legacy-debt"));
            Assert.That(snapshot.Warnings, Does.Contain("keyring-recovery-missing"));
        }
    }

    [Test]
    public async Task GetSnapshotAsync_CountsRecoverySlots()
    {
        string root = CreateTempDirectory();
        var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
        var bootstrap = new KeyringBootstrapService(store);
        var rotation = new KeyringRotationService(store);
        var diagnostics = new KeyringDiagnosticsService(store);
        string unlock = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string recoverySecret = new string('b', 64);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(unlock);

        await bootstrap.OpenOrCreateFromV1Async(settings, unlock);
        await rotation.AddRecoverySlotAsync(unlock, recoverySecret);
        KeyringDiagnosticsSnapshot snapshot = await diagnostics.GetSnapshotAsync(unlock);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.RecoverySlotCount, Is.EqualTo(1));
            Assert.That(snapshot.Warnings, Does.Not.Contain("keyring-recovery-missing"));
        }
    }

    [Test]
    public async Task GetSnapshotAsync_ReportsWrongUnlock()
    {
        string root = CreateTempDirectory();
        var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
        var bootstrap = new KeyringBootstrapService(store);
        var diagnostics = new KeyringDiagnosticsService(store);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringDiagnosticsSnapshot snapshot = await diagnostics.GetSnapshotAsync(
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.UnlockSucceeded, Is.False);
            Assert.That(snapshot.KeyCount, Is.Null);
            Assert.That(snapshot.Warnings, Does.Contain("keyring-unlock-failed"));
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "keyring-diagnostics", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
