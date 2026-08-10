// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Services;
using Cotton.Storage.Backends;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class MasterKeySentinelStoreTests
    {
        private string _storageBasePath = null!;

        [SetUp]
        public void SetUp()
        {
            _storageBasePath = Path.Combine(
                Path.GetTempPath(),
                "cotton-master-key-sentinel-tests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            TestDirectory.Delete(_storageBasePath);
        }

        [Test]
        public async Task ValidateOrInitializeAsync_CreatesAndReusesSentinel()
        {
            MasterKeySentinelStore store = CreateStore();
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            MasterKeySentinelResult created = await store.ValidateOrInitializeAsync(settings);
            MasterKeySentinelResult reused = await store.ValidateOrInitializeAsync(settings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(created.Success, Is.True);
                Assert.That(created.Created, Is.True);
                Assert.That(reused.Success, Is.True);
                Assert.That(reused.Created, Is.False);
            }
        }

        [Test]
        public async Task ValidateOrInitializeAsync_RejectsWrongMasterKey()
        {
            MasterKeySentinelStore store = CreateStore();
            CottonEncryptionSettings original = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            CottonEncryptionSettings wrong = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            await store.ValidateOrInitializeAsync(original);
            MasterKeySentinelResult rejected = await store.ValidateOrInitializeAsync(wrong);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rejected.Success, Is.False);
                Assert.That(rejected.Error, Does.Contain("does not match"));
            }
        }

        [Test]
        public async Task ValidateOrInitializeAsync_RejectsCorruptSentinel()
        {
            FileSystemStorageBackend backend = CreateBackend();
            await backend.WriteAsync(
                MasterKeySentinelStore.SentinelStorageKey,
                new MemoryStream([1, 2, 3]));
            MasterKeySentinelStore store = new(
                NullLogger<MasterKeySentinelStore>.Instance,
                backend);
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            MasterKeySentinelResult result = await store.ValidateOrInitializeAsync(settings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Does.Contain("corrupted"));
            }
        }

        private MasterKeySentinelStore CreateStore()
        {
            return new MasterKeySentinelStore(
                NullLogger<MasterKeySentinelStore>.Instance,
                CreateBackend());
        }

        private FileSystemStorageBackend CreateBackend()
        {
            return new FileSystemStorageBackend(
                NullLogger<FileSystemStorageBackend>.Instance,
                _storageBasePath);
        }
    }
}
