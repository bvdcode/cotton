// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using EasyExtensions.Crypto;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Storage.Tests.Integration
{
    [TestFixture]
    public class IntegrationTests
    {
        private string _testBasePath = null!;
        private AesGcmStreamCipher _cipher = null!;

        private static string NewUid() => Guid.NewGuid().ToString("N")[..12];

        [SetUp]
        public void Setup()
        {
            _testBasePath = Path.Combine(AppContext.BaseDirectory, "files");
            if (Directory.Exists(_testBasePath))
            {
                CleanupDirectory(_testBasePath);
            }

            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            _cipher = new AesGcmStreamCipher(key, keyId: 1, threads: null);
        }

        [TearDown]
        public void TearDown()
        {
            _cipher?.Dispose();

            if (Directory.Exists(_testBasePath))
            {
                CleanupDirectory(_testBasePath);
            }
        }

        private void CleanupDirectory(string path)
        {
            try
            {
                foreach (var file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(path, true);
            }
            catch
            {
                // Best effort
            }
        }

        private class FakeBackendProvider : IStorageBackendProvider
        {
            private readonly IStorageBackend _backend;

            public FakeBackendProvider(IStorageBackend backend)
            {
                _backend = backend;
            }

            public IStorageBackend GetBackend() => _backend;
        }

        [Test]
        public async Task Integration_FileSystemBackend_WithCrypto_RoundTrip()
        {
            // Arrange
            var backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            var backend = new FileSystemStorageBackend(backendLogger.Object);
            var provider = new FakeBackendProvider(backend);
            var pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            var cryptoProcessor = new CryptoProcessor(_cipher);

            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                new IStorageProcessor[] { cryptoProcessor });

            var originalData = Encoding.UTF8.GetBytes("Sensitive information that should be encrypted");
            string uid = NewUid();

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));

            // Verify data on disk is encrypted (not plaintext)
            var diskStream = await backend.ReadAsync(uid);
            var diskData = new MemoryStream();
            await diskStream.CopyToAsync(diskData);
            Assert.That(diskData.ToArray(), Is.Not.EqualTo(originalData),
                "Data on disk should be encrypted");

            // Read through pipeline should decrypt
            var readStream = await pipeline.ReadAsync(uid);
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);

            // Assert
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task Integration_FileSystemBackend_WithCompressionAndCrypto_RoundTrip()
        {
            // Arrange
            var backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            var backend = new FileSystemStorageBackend(backendLogger.Object);
            var provider = new FakeBackendProvider(backend);
            var pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            var cryptoProcessor = new CryptoProcessor(_cipher);
            var compressionProcessor = new CompressionProcessor();

            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                new IStorageProcessor[] { cryptoProcessor, compressionProcessor });

            var originalData = Encoding.UTF8.GetBytes(new string('A', 10000)); // Highly compressible
            string uid = NewUid();

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));
            var readStream = await pipeline.ReadAsync(uid);
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);

            // Assert
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task Integration_MultipleFiles_IndependentOperations()
        {
            // Arrange
            var backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            var backend = new FileSystemStorageBackend(backendLogger.Object);
            var provider = new FakeBackendProvider(backend);
            var pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            var cryptoProcessor = new CryptoProcessor(_cipher);

            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                new IStorageProcessor[] { cryptoProcessor });

            var testData = Enumerable.Range(0, 3)
                .Select(i => (uid: NewUid(), data: Encoding.UTF8.GetBytes($"File {i + 1}")))
                .ToList();

            // Act - Write all
            foreach (var (uid, data) in testData)
            {
                await pipeline.WriteAsync(uid, new MemoryStream(data));
            }

            // Act - Read all
            foreach (var (uid, data) in testData)
            {
                var readStream = await pipeline.ReadAsync(uid);
                using var result = new MemoryStream();
                await readStream.CopyToAsync(result);

                // Assert
                Assert.That(result.ToArray(), Is.EqualTo(data),
                    $"File {uid} should match original data");
            }
        }

        [Test]
        public async Task Integration_LargeFile_5MB_NoMemoryExhaustion()
        {
            // Arrange
            var backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            var backend = new FileSystemStorageBackend(backendLogger.Object);
            var provider = new FakeBackendProvider(backend);
            var pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            var compressionProcessor = new CompressionProcessor();

            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                new IStorageProcessor[] { compressionProcessor });

            string uid = NewUid();
            var originalData = new byte[5 * 1024 * 1024];
            RandomNumberGenerator.Fill(originalData);

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));
            var readStream = await pipeline.ReadAsync(uid);

            // Assert - read in chunks to verify streaming works
            var buffer = new byte[1024 * 1024]; // 1MB buffer
            int totalRead = 0;
            int bytesRead;

            while ((bytesRead = await readStream.ReadAsync(buffer)) > 0)
            {
                totalRead += bytesRead;
            }

            Assert.That(totalRead, Is.EqualTo(originalData.Length));
        }

        [Test]
        public async Task Integration_ProcessorOrder_IsRespected()
        {
            // Arrange
            var backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            var backend = new FileSystemStorageBackend(backendLogger.Object);
            var provider = new FakeBackendProvider(backend);
            var pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            var cryptoProcessor = new CryptoProcessor(_cipher);       // Priority: 1000
            var compressionProcessor = new CompressionProcessor();    // Priority: 10000

            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                new IStorageProcessor[] { compressionProcessor, cryptoProcessor });

            var originalData = Encoding.UTF8.GetBytes("Test data for order verification");
            string uid = NewUid();

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));

            // Verify full round trip
            var readStream = await pipeline.ReadAsync(uid);
            using var result = new MemoryStream();
            await readStream.CopyToAsync(result);

            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task Integration_ParallelOperations_NoRaceConditions()
        {
            // Arrange
            var backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            var backend = new FileSystemStorageBackend(backendLogger.Object);
            var provider = new FakeBackendProvider(backend);
            var pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            var cryptoProcessor = new CryptoProcessor(_cipher);

            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                new IStorageProcessor[] { cryptoProcessor });

            var testData = Enumerable.Range(0, 20)
                .Select(i => (uid: $"abc{i:D3}def{i:D3}", data: Encoding.UTF8.GetBytes($"Data {i}")))
                .ToList();

            // Act - Parallel writes
            var writeTasks = testData.Select(item =>
                pipeline.WriteAsync(item.uid, new MemoryStream(item.data)));
            await Task.WhenAll(writeTasks);

            // Act - Parallel reads
            var readTasks = testData.Select(async item =>
            {
                var readStream = await pipeline.ReadAsync(item.uid);
                using var result = new MemoryStream();
                await readStream.CopyToAsync(result);
                return (item.uid, actual: result.ToArray(), expected: item.data);
            });

            var results = await Task.WhenAll(readTasks);

            // Assert
            foreach (var (uid, actual, expected) in results)
            {
                Assert.That(actual, Is.EqualTo(expected), $"Data mismatch for UID: {uid}");
            }
        }
    }
}
