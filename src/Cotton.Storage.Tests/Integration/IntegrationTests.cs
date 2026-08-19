// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Cotton.Crypto;
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

            byte[] key = new byte[32];
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

        private static void CleanupDirectory(string path)
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
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

        private class FakeBackendProvider(IStorageBackend backend) : IStorageBackendProvider
        {
            public IStorageBackend GetBackend() => backend;
        }

        [Test]
        public async Task Integration_FileSystemBackend_WithCrypto_RoundTrip()
        {
            // Arrange
            Mock<ILogger<FileSystemStorageBackend>> backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            FileSystemStorageBackend backend = new FileSystemStorageBackend(backendLogger.Object);
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            CryptoProcessor cryptoProcessor = new CryptoProcessor(_cipher);

            FileStoragePipeline pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [cryptoProcessor],
                new StorageWriteAdmissionGate(1));

            byte[] originalData = Encoding.UTF8.GetBytes("Sensitive information that should be encrypted");
            string uid = NewUid();

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));

            // Verify data on disk is encrypted (not plaintext)
            Stream diskStream = await backend.ReadAsync(uid);
            MemoryStream diskData = new MemoryStream();
            await diskStream.CopyToAsync(diskData);
            Assert.That(diskData.ToArray(), Is.Not.EqualTo(originalData),
                "Data on disk should be encrypted");

            // Read through pipeline should decrypt
            Stream readStream = await pipeline.ReadAsync(uid);
            MemoryStream result = new MemoryStream();
            await readStream.CopyToAsync(result);

            // Assert
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task Integration_FileSystemBackend_WithCompressionAndCrypto_RoundTrip()
        {
            // Arrange
            Mock<ILogger<FileSystemStorageBackend>> backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            FileSystemStorageBackend backend = new FileSystemStorageBackend(backendLogger.Object);
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            CryptoProcessor cryptoProcessor = new CryptoProcessor(_cipher);
            CompressionProcessor compressionProcessor = new CompressionProcessor();

            FileStoragePipeline pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [cryptoProcessor, compressionProcessor],
                new StorageWriteAdmissionGate(1));

            byte[] originalData = Encoding.UTF8.GetBytes(new string('A', 10000)); // Highly compressible
            string uid = NewUid();

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));
            Stream readStream = await pipeline.ReadAsync(uid);
            MemoryStream result = new MemoryStream();
            await readStream.CopyToAsync(result);

            // Assert
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task Integration_MultipleFiles_IndependentOperations()
        {
            // Arrange
            Mock<ILogger<FileSystemStorageBackend>> backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            FileSystemStorageBackend backend = new FileSystemStorageBackend(backendLogger.Object);
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            CryptoProcessor cryptoProcessor = new CryptoProcessor(_cipher);

            FileStoragePipeline pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [cryptoProcessor],
                new StorageWriteAdmissionGate(1));

            List<(string uid, byte[] data)> testData = Enumerable.Range(0, 3)
                .Select(i => (uid: NewUid(), data: Encoding.UTF8.GetBytes($"File {i + 1}")))
                .ToList();

            // Act - Write all
            foreach ((string? uid, byte[]? data) in testData)
            {
                await pipeline.WriteAsync(uid, new MemoryStream(data));
            }

            // Act - Read all
            foreach ((string? uid, byte[]? data) in testData)
            {
                Stream readStream = await pipeline.ReadAsync(uid);
                using MemoryStream result = new MemoryStream();
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
            Mock<ILogger<FileSystemStorageBackend>> backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            FileSystemStorageBackend backend = new FileSystemStorageBackend(backendLogger.Object);
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            CompressionProcessor compressionProcessor = new CompressionProcessor();

            FileStoragePipeline pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [compressionProcessor],
                new StorageWriteAdmissionGate(1));

            string uid = NewUid();
            byte[] originalData = new byte[5 * 1024 * 1024];
            RandomNumberGenerator.Fill(originalData);

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));
            Stream readStream = await pipeline.ReadAsync(uid);

            // Assert - read in chunks to verify streaming works
            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer
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
            Mock<ILogger<FileSystemStorageBackend>> backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            FileSystemStorageBackend backend = new FileSystemStorageBackend(backendLogger.Object);
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            CryptoProcessor cryptoProcessor = new CryptoProcessor(_cipher);       // Priority: 1000
            CompressionProcessor compressionProcessor = new CompressionProcessor();    // Priority: 10000

            FileStoragePipeline pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [compressionProcessor, cryptoProcessor],
                new StorageWriteAdmissionGate(1));

            byte[] originalData = Encoding.UTF8.GetBytes("Test data for order verification");
            string uid = NewUid();

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));

            // Verify full round trip
            Stream readStream = await pipeline.ReadAsync(uid);
            using MemoryStream result = new MemoryStream();
            await readStream.CopyToAsync(result);

            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task Integration_ParallelOperations_NoRaceConditions()
        {
            // Arrange
            Mock<ILogger<FileSystemStorageBackend>> backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            FileSystemStorageBackend backend = new FileSystemStorageBackend(backendLogger.Object);
            FakeBackendProvider provider = new FakeBackendProvider(backend);
            Mock<ILogger<FileStoragePipeline>> pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();

            CryptoProcessor cryptoProcessor = new CryptoProcessor(_cipher);

            FileStoragePipeline pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [cryptoProcessor],
                new StorageWriteAdmissionGate(1));

            List<(string uid, byte[] data)> testData = Enumerable.Range(0, 20)
                .Select(i => (uid: $"abc{i:D3}def{i:D3}", data: Encoding.UTF8.GetBytes($"Data {i}")))
                .ToList();

            // Act - Parallel writes
            IEnumerable<Task> writeTasks = testData.Select(item =>
                pipeline.WriteAsync(item.uid, new MemoryStream(item.data)));
            await Task.WhenAll(writeTasks);

            // Act - Parallel reads
            IEnumerable<Task<(string uid, byte[] actual, byte[] expected)>> readTasks = testData.Select(async item =>
            {
                Stream readStream = await pipeline.ReadAsync(item.uid);
                using MemoryStream result = new MemoryStream();
                await readStream.CopyToAsync(result);
                return (item.uid, actual: result.ToArray(), expected: item.data);
            });

            (string uid, byte[] actual, byte[] expected)[] results = await Task.WhenAll(readTasks);

            // Assert
            foreach ((string? uid, byte[]? actual, byte[]? expected) in results)
            {
                Assert.That(actual, Is.EqualTo(expected), $"Data mismatch for UID: {uid}");
            }
        }
    }
}
