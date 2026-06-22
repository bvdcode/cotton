// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Cotton.Storage.Processors;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class MasterKeyCompatibilityProbeTests : IntegrationTestBase
    {
        private string _storageBasePath = null!;

        [SetUp]
        public void SetUp()
        {
            _storageBasePath = Path.Combine(Path.GetTempPath(), "cotton-master-key-probe-tests", Guid.NewGuid().ToString("N"));
            DbContext.Database.EnsureDeleted();
            DbContext.Database.Migrate();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_storageBasePath))
            {
                Directory.Delete(_storageBasePath, recursive: true);
            }
        }

        [Test]
        public async Task ValidateAsync_Accepts_Key_That_Decrypts_Existing_Storage_Chunk()
        {
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            await StoreEncryptedChunkAsync(settings);
            var probe = CreateProbe();

            MasterKeyCompatibilityResult result = await probe.ValidateAsync(
                settings,
                MasterKeyCompatibilityMode.RequireEvidenceForExistingData);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.ExistingDataFound, Is.True);
                Assert.That(result.EvidenceFound, Is.True);
            }
        }

        [Test]
        public async Task ValidateAsync_Skips_Corrupt_Database_Probe_And_Accepts_Storage_Chunk()
        {
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            DbContext.Users.Add(new User
            {
                Username = "corruptdb" + Guid.NewGuid().ToString("N")[..8],
                PasswordPhc = "phc",
                WebDavTokenPhc = "webdav",
                Role = UserRole.Admin,
                AvatarHashEncrypted = RandomNumberGenerator.GetBytes(48)
            });
            await DbContext.SaveChangesAsync();
            await StoreEncryptedChunkAsync(settings);
            var probe = CreateProbe();

            MasterKeyCompatibilityResult result = await probe.ValidateAsync(
                settings,
                MasterKeyCompatibilityMode.RequireEvidenceForExistingData);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.ExistingDataFound, Is.True);
                Assert.That(result.EvidenceFound, Is.True);
            }
        }

        [Test]
        public async Task ValidateAsync_Does_Not_Touch_Storage_When_Encrypted_Configuration_Probe_Fails()
        {
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            DbContext.Users.Add(new User
            {
                Username = "corruptdb" + Guid.NewGuid().ToString("N")[..8],
                PasswordPhc = "phc",
                WebDavTokenPhc = "webdav",
                Role = UserRole.Admin,
                AvatarHashEncrypted = RandomNumberGenerator.GetBytes(48)
            });
            await DbContext.SaveChangesAsync();
            var probe = CreateProbe(new ThrowingEncryptedConfigurationStorageBackend());

            MasterKeyCompatibilityResult result = await probe.ValidateAsync(
                settings,
                MasterKeyCompatibilityMode.RequireEvidenceForExistingData);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ExistingDataFound, Is.True);
                Assert.That(result.EvidenceFound, Is.True);
                Assert.That(result.Error, Does.Contain("does not match"));
            }
        }

        [Test]
        public async Task ValidateAsync_Skips_Corrupt_Storage_Probe_And_Accepts_Later_Chunk()
        {
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            await StoreRawChunkAsync(RandomNumberGenerator.GetBytes(12), plainSizeBytes: 12);
            await StoreEncryptedChunkAsync(settings);
            var probe = CreateProbe();

            MasterKeyCompatibilityResult result = await probe.ValidateAsync(
                settings,
                MasterKeyCompatibilityMode.RequireEvidenceForExistingData);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.ExistingDataFound, Is.True);
                Assert.That(result.EvidenceFound, Is.True);
            }
        }

        [Test]
        public async Task ValidateAsync_Rejects_Key_That_Cannot_Decrypt_Existing_Storage_Chunk()
        {
            CottonEncryptionSettings original = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            CottonEncryptionSettings wrong = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            await StoreEncryptedChunkAsync(original);
            var probe = CreateProbe();

            MasterKeyCompatibilityResult result = await probe.ValidateAsync(
                wrong,
                MasterKeyCompatibilityMode.RequireEvidenceForExistingData);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.EvidenceFound, Is.True);
                Assert.That(result.Error, Does.Contain("does not match"));
            }
        }

        [Test]
        public async Task ValidateAsync_Requires_Evidence_For_Initialized_Database_Without_Probe_Data()
        {
            DbContext.Users.Add(new User
            {
                Username = "probeuser",
                PasswordPhc = "phc",
                WebDavTokenPhc = "webdav",
                Role = UserRole.Admin
            });
            await DbContext.SaveChangesAsync();
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            var probe = CreateProbe();

            MasterKeyCompatibilityResult result = await probe.ValidateAsync(
                settings,
                MasterKeyCompatibilityMode.RequireEvidenceForExistingData);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ExistingDataFound, Is.True);
                Assert.That(result.EvidenceFound, Is.False);
            }
        }

        private MasterKeyCompatibilityProbe CreateProbe(IStorageBackend? storage = null)
        {
            string connectionString = DbContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Test database connection string is not configured.");
            return new MasterKeyCompatibilityProbe(
                NullLogger<MasterKeyCompatibilityProbe>.Instance,
                storage ?? CreateStorageBackend(),
                connectionString);
        }

        private IStorageBackend CreateStorageBackend() =>
            new FileSystemStorageBackend(NullLogger<FileSystemStorageBackend>.Instance, _storageBasePath);

        private class ThrowingEncryptedConfigurationStorageBackend : IStorageBackend, IStorageBackendUsesEncryptedConfiguration
        {
            public void CleanupTempFiles(TimeSpan ttl) => throw StorageTouched();
            public Task<bool> DeleteAsync(string uid) => throw StorageTouched();
            public Task<bool> ExistsAsync(string uid) => throw StorageTouched();
            public Task<long> GetSizeAsync(string uid) => throw StorageTouched();
            public Task<Stream> ReadAsync(string uid) => throw StorageTouched();
            public Task WriteAsync(string uid, Stream stream) => throw StorageTouched();
            public IAsyncEnumerable<string> ListAllKeysAsync(CancellationToken ct = default) => throw StorageTouched();

            private static InvalidOperationException StorageTouched() =>
                new("Encrypted configuration storage should not be touched after failed database proof.");
        }

        private async Task StoreEncryptedChunkAsync(CottonEncryptionSettings settings)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes("probe plaintext");
            await using var plaintext = new MemoryStream(plainBytes, writable: false);
            using var cipher = new AesGcmStreamCipher(
                Convert.FromBase64String(settings.MasterEncryptionKey),
                settings.MasterEncryptionKeyId,
                settings.EncryptionThreads > 0 ? settings.EncryptionThreads : null);
            await using Stream encrypted = await cipher.EncryptAsync(plaintext);
            await StoreChunkAsync(encrypted, plainBytes.Length);
        }

        private async Task StoreRawChunkAsync(byte[] storedBytes, long plainSizeBytes)
        {
            await using var stream = new MemoryStream(storedBytes, writable: false);
            await StoreChunkAsync(stream, plainSizeBytes);
        }

        private async Task StoreChunkAsync(Stream stream, long plainSizeBytes)
        {
            byte[] hash = Hasher.HashData(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));
            string storageKey = Hasher.ToHexStringHash(hash);
            var backend = new FileSystemStorageBackend(
                NullLogger<FileSystemStorageBackend>.Instance,
                _storageBasePath);

            await backend.WriteAsync(storageKey, stream);
            long storedSize = await backend.GetSizeAsync(storageKey);

            DbContext.Chunks.Add(new Chunk
            {
                Hash = hash,
                PlainSizeBytes = plainSizeBytes,
                StoredSizeBytes = storedSize,
                CompressionAlgorithm = CompressionProcessor.Algorithm,
                GCScheduledAfter = null
            });
            await DbContext.SaveChangesAsync();
        }
    }
}
