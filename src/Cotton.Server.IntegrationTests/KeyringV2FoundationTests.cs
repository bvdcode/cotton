// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Services.KeyManagement;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace Cotton.Server.IntegrationTests;

public class KeyringV2FoundationTests
{
    [Test]
    public void InitialState_MarksLegacyChunkKeyDecryptOnly_AndRandomChunkKeyPrimary()
    {
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        Guid instanceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        DateTimeOffset now = DateTimeOffset.Parse("2026-05-27T00:00:00Z");

        KeyringPlainState state = KeyringV1UpgradeBuilder.CreateInitialState(
            settings,
            instanceId,
            keyringId: "test-keyring",
            createdAtUtc: now);

        KeyringKeyRecord legacyChunk = state.Keys.Single(x =>
            x.Purpose == KeyringKeyPurpose.ChunkAead && x.Id == KeyringV1UpgradeBuilder.LegacyKeyId);
        KeyringKeyRecord v2Chunk = state.Keys.Single(x =>
            x.Purpose == KeyringKeyPurpose.ChunkAead && x.Id == KeyringV1UpgradeBuilder.FirstV2ChunkKeyId);
        KeyringKeyRecord pepper = state.Keys.Single(x => x.Purpose == KeyringKeyPurpose.PasswordPepper);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.Primary.ChunkAead, Is.EqualTo(KeyringV1UpgradeBuilder.FirstV2ChunkKeyId));
            Assert.That(legacyChunk.Status, Is.EqualTo(KeyringKeyStatus.DecryptOnly));
            Assert.That(legacyChunk.Origin, Is.EqualTo(KeyringKeyOrigin.LegacyV1MasterDerived));
            Assert.That(v2Chunk.Status, Is.EqualTo(KeyringKeyStatus.EncryptDecrypt));
            Assert.That(v2Chunk.Origin, Is.EqualTo(KeyringKeyOrigin.RandomV2));
            Assert.That(pepper.MaterialBase64, Is.EqualTo(settings.Pepper));
        }
    }

    [Test]
    public void AccessEnvelope_UnwrapsRootKey_WithCorrectSecret_AndRejectsWrongSecret()
    {
        byte[] rootKey = KeyringCryptography.GenerateKeyMaterial();
        try
        {
            KeyringAccessEnvelope envelope = KeyringCryptography.CreateLegacyMasterAccessEnvelope(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "test-keyring",
                rootEpoch: 1,
                generation: 1,
                parentHash: null,
                rootKey,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "master:default",
                DateTimeOffset.Parse("2026-05-27T00:00:00Z"));

            KeyringUnwrapResult correct = KeyringCryptography.TryUnwrapRootKey(
                envelope,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            KeyringUnwrapResult wrong = KeyringCryptography.TryUnwrapRootKey(
                envelope,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(correct.Success, Is.True);
                Assert.That(correct.KeyringRootKey, Is.EqualTo(rootKey));
                Assert.That(correct.SlotId, Is.EqualTo("master:default"));
                Assert.That(wrong.Success, Is.False);
                Assert.That(wrong.KeyringRootKey, Is.Null);
            }
        }
        finally
        {
            Array.Clear(rootKey);
        }
    }

    [Test]
    public void StateSnapshot_ProtectsAndRestoresPlainState()
    {
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringPlainState state = KeyringV1UpgradeBuilder.CreateInitialState(
            settings,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            keyringId: "test-keyring",
            createdAtUtc: DateTimeOffset.Parse("2026-05-27T00:00:00Z"));
        byte[] rootKey = KeyringCryptography.GenerateKeyMaterial();
        byte[] wrongRootKey = KeyringCryptography.GenerateKeyMaterial();
        try
        {
            KeyringStateSnapshot snapshot = KeyringCryptography.ProtectState(state, rootKey);
            KeyringPlainState restored = KeyringCryptography.UnprotectState(snapshot, rootKey);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(KeyringJson.SerializeToUtf8Bytes(restored), Is.EqualTo(KeyringJson.SerializeToUtf8Bytes(state)));
                Assert.That(
                    () => KeyringCryptography.UnprotectState(snapshot, wrongRootKey),
                    Throws.InstanceOf<System.Security.Cryptography.CryptographicException>());
            }
        }
        finally
        {
            Array.Clear(rootKey);
            Array.Clear(wrongRootKey);
        }
    }

    [Test]
    public async Task JournaledStore_UsesHeadsInsteadOfLatestCache()
    {
        var replica = new MemoryKeyringReplica("primary");
        var store = new KeyringJournaledObjectStore([replica]);
        byte[] generation1 = [1, 2, 3];
        byte[] generation2 = [4, 5, 6];

        await store.CommitAsync(KeyringObjectKind.StateSnapshot, generation: 1, generation1);
        KeyringObjectPointer second = await store.CommitAsync(KeyringObjectKind.StateSnapshot, generation: 2, generation2);
        await replica.WriteAsync(KeyringObjectNames.GetLatestName(KeyringObjectKind.StateSnapshot), [9, 9, 9]);

        KeyringLoadedObject? latest = await store.FindLatestValidAsync(KeyringObjectKind.StateSnapshot);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(latest, Is.Not.Null);
            Assert.That(latest!.Pointer, Is.EqualTo(second));
            Assert.That(latest.Bytes, Is.EqualTo(generation2));
        }
    }

    [Test]
    public async Task JournaledStore_RecoversLatestObject_FromHealthyReplica()
    {
        var damaged = new MemoryKeyringReplica("damaged");
        var healthy = new MemoryKeyringReplica("healthy");
        var store = new KeyringJournaledObjectStore([damaged, healthy]);
        byte[] payload = [1, 2, 3, 4];

        KeyringObjectPointer pointer = await store.CommitAsync(KeyringObjectKind.AccessEnvelope, generation: 1, payload);
        damaged.Overwrite(pointer.ObjectName, [8, 8, 8]);

        KeyringLoadedObject? latest = await store.FindLatestValidAsync(KeyringObjectKind.AccessEnvelope);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(latest, Is.Not.Null);
            Assert.That(latest!.ReplicaName, Is.EqualTo("healthy"));
            Assert.That(latest.Bytes, Is.EqualTo(payload));
        }
    }

    private sealed class MemoryKeyringReplica(string name) : IKeyringObjectReplica
    {
        private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

        public string Name { get; } = name;

        public Task WriteAsync(string name, byte[] bytes, CancellationToken cancellationToken = default)
        {
            _objects[name] = bytes.ToArray();
            return Task.CompletedTask;
        }

        public Task<byte[]?> TryReadAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_objects.TryGetValue(name, out byte[]? bytes) ? bytes.ToArray() : null);
        }

        public async IAsyncEnumerable<string> ListNamesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (string key in _objects.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return key;
            }

            await Task.CompletedTask;
        }

        public void Overwrite(string name, byte[] bytes)
        {
            _objects[name] = bytes.ToArray();
        }
    }
}
