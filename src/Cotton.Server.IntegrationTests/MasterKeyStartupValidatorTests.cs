// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class MasterKeyStartupValidatorTests
    {
        private const string CorrectRootKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string WrongRootKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private string _storageBasePath = null!;

        [SetUp]
        public void SetUp()
        {
            _storageBasePath = Path.Combine(
                Path.GetTempPath(),
                "cotton-master-key-startup-tests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            TestDirectory.Delete(_storageBasePath);
        }

        [Test]
        public async Task ValidateAsync_AcceptsExistingSentinel()
        {
            FileSystemStorageBackend backend = CreateBackend();
            CottonEncryptionSettings settings = CreateSettings(CorrectRootKey);
            MasterKeySentinelStore sentinel = CreateSentinel(backend);
            MasterKeySentinelResult created = await sentinel.ValidateOrInitializeAsync(settings);
            Assert.That(created.Success, Is.True);
            using AesGcmStreamCipher cipher = StreamCipherFactory.Create(settings);
            MasterKeyStartupValidator validator = CreateValidator(backend, cipher, settings);

            await validator.ValidateAsync();
        }

        [Test]
        public async Task ValidateAsync_RejectsWrongKeyForExistingSentinel()
        {
            FileSystemStorageBackend backend = CreateBackend();
            CottonEncryptionSettings correctSettings = CreateSettings(CorrectRootKey);
            await CreateSentinel(backend).ValidateOrInitializeAsync(correctSettings);
            CottonEncryptionSettings wrongSettings = CreateSettings(WrongRootKey);
            using AesGcmStreamCipher cipher = StreamCipherFactory.Create(wrongSettings);
            MasterKeyStartupValidator validator = CreateValidator(backend, cipher, wrongSettings);

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await validator.ValidateAsync());

            Assert.That(exception!.Message, Does.Contain("does not match"));
        }

        [Test]
        public async Task ValidateAsync_CreatesSentinelAfterStorageEvidenceMatches()
        {
            FileSystemStorageBackend backend = CreateBackend();
            CottonEncryptionSettings settings = CreateSettings(CorrectRootKey);
            using AesGcmStreamCipher cipher = StreamCipherFactory.Create(settings);
            await StoreEncryptedObjectAsync(backend, cipher, "abcdef", [1, 2, 3]);
            MasterKeyStartupValidator validator = CreateValidator(backend, cipher, settings);

            await validator.ValidateAsync();

            Assert.That(
                await backend.ExistsAsync(MasterKeySentinelStore.SentinelStorageKey),
                Is.True);
        }

        [Test]
        public async Task ValidateAsync_RejectsKeyWhenStorageEvidenceDoesNotMatch()
        {
            FileSystemStorageBackend backend = CreateBackend();
            CottonEncryptionSettings correctSettings = CreateSettings(CorrectRootKey);
            using (AesGcmStreamCipher correctCipher = StreamCipherFactory.Create(correctSettings))
            {
                await StoreEncryptedObjectAsync(backend, correctCipher, "abcdef", [1, 2, 3]);
            }
            CottonEncryptionSettings wrongSettings = CreateSettings(WrongRootKey);
            using AesGcmStreamCipher wrongCipher = StreamCipherFactory.Create(wrongSettings);
            MasterKeyStartupValidator validator = CreateValidator(backend, wrongCipher, wrongSettings);

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await validator.ValidateAsync());
            Assert.That(exception!.Message, Does.Contain("does not match"));
            Assert.That(
                await backend.ExistsAsync(MasterKeySentinelStore.SentinelStorageKey),
                Is.False);
        }

        [Test]
        public async Task ValidateAsync_DoesNotBypassCorruptSentinelWithValidStorageEvidence()
        {
            FileSystemStorageBackend backend = CreateBackend();
            await backend.WriteAsync(
                MasterKeySentinelStore.SentinelStorageKey,
                new MemoryStream([1, 2, 3]));
            CottonEncryptionSettings settings = CreateSettings(CorrectRootKey);
            using AesGcmStreamCipher cipher = StreamCipherFactory.Create(settings);
            await StoreEncryptedObjectAsync(backend, cipher, "abcdef", [4, 5, 6]);
            MasterKeyStartupValidator validator = CreateValidator(backend, cipher, settings);

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await validator.ValidateAsync());

            Assert.That(exception!.Message, Does.Contain("corrupted"));
        }

        [Test]
        public async Task ValidateAsync_CreatesSentinelForEmptyStorage()
        {
            FileSystemStorageBackend backend = CreateBackend();
            CottonEncryptionSettings settings = CreateSettings(CorrectRootKey);
            using AesGcmStreamCipher cipher = StreamCipherFactory.Create(settings);
            MasterKeyStartupValidator validator = CreateValidator(backend, cipher, settings);

            await validator.ValidateAsync();

            Assert.That(
                await backend.ExistsAsync(MasterKeySentinelStore.SentinelStorageKey),
                Is.True);
        }

        [Test]
        public async Task ValidateAsync_ValidatesEncryptedConfigurationBackendThroughStorage()
        {
            FileSystemStorageBackend innerBackend = CreateBackend();
            EncryptedConfigurationStorageBackend backend = new(innerBackend);
            CottonEncryptionSettings correctSettings = CreateSettings(CorrectRootKey);
            using (AesGcmStreamCipher correctCipher = StreamCipherFactory.Create(correctSettings))
            {
                MasterKeyStartupValidator validator = CreateValidator(backend, correctCipher, correctSettings);
                await validator.ValidateAsync();
            }

            CottonEncryptionSettings wrongSettings = CreateSettings(WrongRootKey);
            using AesGcmStreamCipher wrongCipher = StreamCipherFactory.Create(wrongSettings);
            MasterKeyStartupValidator wrongKeyValidator = CreateValidator(backend, wrongCipher, wrongSettings);

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await wrongKeyValidator.ValidateAsync());
            Assert.That(exception!.Message, Does.Contain("does not match"));
        }

        private FileSystemStorageBackend CreateBackend()
        {
            return new FileSystemStorageBackend(
                NullLogger<FileSystemStorageBackend>.Instance,
                _storageBasePath);
        }

        private static CottonEncryptionSettings CreateSettings(string rootKey)
        {
            return ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootKey);
        }

        private static MasterKeySentinelStore CreateSentinel(IStorageBackend backend)
        {
            return new MasterKeySentinelStore(
                NullLogger<MasterKeySentinelStore>.Instance,
                backend);
        }

        private static MasterKeyStartupValidator CreateValidator(
            IStorageBackend backend,
            IStreamCipher cipher,
            CottonEncryptionSettings settings)
        {
            return new MasterKeyStartupValidator(
                new StaticStorageBackendProvider(backend),
                cipher,
                settings,
                NullLogger<MasterKeySentinelStore>.Instance,
                NullLogger<MasterKeyStartupValidator>.Instance);
        }

        private static async Task StoreEncryptedObjectAsync(
            IStorageBackend backend,
            IStreamCipher cipher,
            string storageKey,
            byte[] plaintext)
        {
            await using MemoryStream source = new(plaintext, writable: false);
            await using Stream encrypted = await cipher.EncryptAsync(source);
            await backend.WriteAsync(storageKey, encrypted);
        }
    }
}
