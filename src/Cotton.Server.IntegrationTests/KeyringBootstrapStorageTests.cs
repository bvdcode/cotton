// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Services.KeyManagement;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class KeyringBootstrapStorageTests
{
    [Test]
    public async Task LocalFileReplica_WritesReadsLists_AndRejectsTraversal()
    {
        string root = CreateTempDirectory();
        var replica = new KeyringLocalFileReplica(root, "test-local");
        string objectName = ".cotton/system/keyring/v2/state/00000000000000000001-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.json";
        byte[] bytes = [1, 2, 3, 4];

        await replica.WriteAsync(objectName, bytes);
        byte[]? read = await replica.TryReadAsync(objectName);
        List<string> names = await replica.ListNamesAsync().ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(read, Is.EqualTo(bytes));
            Assert.That(names, Does.Contain(objectName));
            Assert.That(
                async () => await replica.WriteAsync("../escape.json", bytes),
                Throws.ArgumentException);
        }
    }

    [Test]
    public async Task Bootstrap_CreatesInitialKeyring_ThenReopensExistingState()
    {
        string root = CreateTempDirectory();
        var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
        var bootstrap = new KeyringBootstrapService(store);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        Guid instanceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        KeyringBootstrapResult created = await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            instanceId);
        KeyringBootstrapResult reopened = await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Created, Is.True);
            Assert.That(reopened.Created, Is.False);
            Assert.That(reopened.UnlockedSlotId, Is.EqualTo(KeyringBootstrapService.DefaultLegacyMasterSlotId));
            Assert.That(reopened.State.KeyringId, Is.EqualTo(created.State.KeyringId));
            Assert.That(reopened.State.Primary.ChunkAead, Is.EqualTo(KeyringV1UpgradeBuilder.FirstV2ChunkKeyId));
            Assert.That(
                KeyringJson.SerializeToUtf8Bytes(reopened.State),
                Is.EqualTo(KeyringJson.SerializeToUtf8Bytes(created.State)));
        }
    }

    [Test]
    public async Task Bootstrap_RejectsWrongUnlockSecret_WhenCompleteKeyringExists()
    {
        string root = CreateTempDirectory();
        var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
        var bootstrap = new KeyringBootstrapService(store);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        Guid instanceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            instanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                async () => await bootstrap.OpenOrCreateFromV1Async(
                    settings,
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    instanceId),
                Throws.InstanceOf<UnauthorizedAccessException>());

            KeyringBootstrapResult stillReopens = await bootstrap.OpenOrCreateFromV1Async(
                settings,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                instanceId);
            Assert.That(stillReopens.Created, Is.False);
        }
    }

    [Test]
    public async Task Bootstrap_ReopensExistingKeyring_WithoutExpectedInstanceId()
    {
        string root = CreateTempDirectory();
        var store = new KeyringJournaledObjectStore([new KeyringLocalFileReplica(root)]);
        var bootstrap = new KeyringBootstrapService(store);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        KeyringBootstrapResult created = await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringBootstrapResult reopened = await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Created, Is.True);
            Assert.That(reopened.Created, Is.False);
            Assert.That(reopened.State.InstanceId, Is.EqualTo(created.State.InstanceId));
            Assert.That(reopened.State.KeyringId, Is.EqualTo(created.State.KeyringId));
        }
    }

    [Test]
    [NonParallelizable]
    public async Task KeyringStartup_BootstrapsOnlyWhenEnabled_AndUsesConfiguredPath()
    {
        string root = CreateTempDirectory();
        string? originalEnabled = Environment.GetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable);
        string? originalPath = Environment.GetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        try
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, root);
            Assert.That(
                await KeyringStartup.BootstrapIfEnabledAsync(settings, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                Is.Null);

            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, "1");
            KeyringBootstrapResult? created = await KeyringStartup.BootstrapIfEnabledAsync(
                settings,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            KeyringBootstrapResult? reopened = await KeyringStartup.BootstrapIfEnabledAsync(
                settings,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(created, Is.Not.Null);
                Assert.That(created!.Created, Is.True);
                Assert.That(reopened, Is.Not.Null);
                Assert.That(reopened!.Created, Is.False);
                Assert.That(reopened.State.KeyringId, Is.EqualTo(created.State.KeyringId));
                Assert.That(
                    Directory.EnumerateFiles(root, "*.head", SearchOption.AllDirectories),
                    Is.Not.Empty);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyringStartup.EnabledEnvironmentVariable, originalEnabled);
            Environment.SetEnvironmentVariable(KeyringStartup.KeyringPathEnvironmentVariable, originalPath);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "keyring", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
