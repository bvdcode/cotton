// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Services.KeyManagement;
using Cotton.Storage.Abstractions;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace Cotton.Server.IntegrationTests;

public class KeyringReplicaTests
{
    [Test]
    public async Task StorageBackendReplica_WritesReadsLists_AndOverwritesMutableNames()
    {
        var backend = new MemoryStorageBackend();
        var replica = new KeyringStorageBackendReplica(backend, "storage-test");
        string objectName = KeyringObjectNames.GetLatestName(KeyringObjectKind.StateSnapshot);

        await replica.WriteAsync(objectName, [1, 2, 3]);
        await replica.WriteAsync(objectName, [4, 5, 6]);
        byte[]? read = await replica.TryReadAsync(objectName);
        List<string> names = await replica.ListNamesAsync().ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(read, Is.EqualTo(new byte[] { 4, 5, 6 }));
            Assert.That(names, Does.Contain(objectName));
        }
    }

    [Test]
    public async Task StorageBackendReplica_IgnoresNonKeyringStorageObjects_WhenListing()
    {
        var backend = new MemoryStorageBackend();
        var replica = new KeyringStorageBackendReplica(backend, "storage-test");
        await backend.WriteAsync(new string('a', 64), new MemoryStream([9, 9, 9]));
        string objectName = KeyringObjectNames.GetHeadName(
            KeyringObjectKind.AccessEnvelope,
            generation: 1,
            new string('b', 64));

        await replica.WriteAsync(objectName, [1]);
        List<string> names = await replica.ListNamesAsync().ToListAsync();

        Assert.That(names, Is.EqualTo(new[] { objectName }));
    }


    [Test]
    public async Task JournaledStore_RepairsLatestObjectsIntoMissingReplica()
    {
        string primaryRoot = CreateTempDirectory();
        string secondaryRoot = CreateTempDirectory();
        var primaryReplica = new KeyringLocalFileReplica(primaryRoot, "primary");
        var secondaryReplica = new KeyringLocalFileReplica(secondaryRoot, "secondary");
        var primaryStore = new KeyringJournaledObjectStore([primaryReplica]);
        var bootstrap = new KeyringBootstrapService(primaryStore);
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringBootstrapResult created = await bootstrap.OpenOrCreateFromV1Async(
            settings,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var repairStore = new KeyringJournaledObjectStore([primaryReplica, secondaryReplica]);

        IReadOnlyList<KeyringObjectPointer> repaired = await repairStore.RepairLatestAsync();
        KeyringLoadedObject? reopened = await repairStore.FindLatestValidAsync(KeyringObjectKind.StateSnapshot);
        List<string> secondaryNames = await secondaryReplica.ListNamesAsync().ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(repaired, Has.Count.EqualTo(2));
            Assert.That(reopened, Is.Not.Null);
            Assert.That(reopened!.Pointer.Hash, Is.EqualTo(created.StatePointer.Hash));
            Assert.That(secondaryNames, Does.Contain(created.StatePointer.ObjectName));
            Assert.That(secondaryNames, Does.Contain(created.AccessPointer.ObjectName));
            Assert.That(secondaryNames, Does.Contain(KeyringObjectNames.GetLatestName(KeyringObjectKind.StateSnapshot)));
            Assert.That(secondaryNames, Does.Contain(KeyringObjectNames.GetLatestName(KeyringObjectKind.AccessEnvelope)));
        }
    }


    private static string CreateTempDirectory()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "keyring-replicas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class MemoryStorageBackend : IStorageBackend
    {
        private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.OrdinalIgnoreCase);

        public void CleanupTempFiles(TimeSpan ttl)
        {
        }

        public Task<bool> DeleteAsync(string uid)
        {
            return Task.FromResult(_objects.TryRemove(uid, out _));
        }

        public Task<bool> ExistsAsync(string uid)
        {
            return Task.FromResult(_objects.ContainsKey(uid));
        }

        public Task<long> GetSizeAsync(string uid)
        {
            return Task.FromResult(_objects.TryGetValue(uid, out byte[]? bytes) ? bytes.Length : 0L);
        }

        public Task<Stream> ReadAsync(string uid)
        {
            if (!_objects.TryGetValue(uid, out byte[]? bytes))
            {
                throw new FileNotFoundException("Missing test object", uid);
            }

            return Task.FromResult<Stream>(new MemoryStream(bytes.ToArray(), writable: false));
        }

        public async Task WriteAsync(string uid, Stream stream)
        {
            using var memory = new MemoryStream();
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            await stream.CopyToAsync(memory);
            _objects[uid] = memory.ToArray();
        }

        public async IAsyncEnumerable<string> ListAllKeysAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (string key in _objects.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                yield return key;
            }

            await Task.CompletedTask;
        }
    }
}
