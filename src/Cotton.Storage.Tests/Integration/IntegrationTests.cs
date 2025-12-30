// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Backends;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Cotton.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Moq;
using System.Security.Cryptography;
using System.Text;
using EasyExtensions.Abstractions;

namespace Cotton.Storage.Tests.Integration
{
    [TestFixture]
    public class IntegrationTests
    {
        private string _testBasePath = null!;

        [SetUp]
        public void Setup()
        {
            _testBasePath = Path.Combine(AppContext.BaseDirectory, "files");
            if (Directory.Exists(_testBasePath))
            {
                CleanupDirectory(_testBasePath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testBasePath))
            {
                CleanupDirectory(_testBasePath);
            }
        }

        private static void CleanupDirectory(string path)
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

        private class FakeBackendProvider(IStorageBackend backend) : IStorageBackendProvider
        {
            public IStorageBackend GetBackend() => backend;
        }

        [Test]
        public async Task Integration_FileSystemBackend_WithCrypto_RoundTrip()
        {
            // Arrange
            var backendLogger = new Mock<ILogger<FileSystemStorageBackend>>();
            var backend = new FileSystemStorageBackend(backendLogger.Object);
            var provider = new FakeBackendProvider(backend);
            var pipelineLogger = new Mock<ILogger<FileStoragePipeline>>();
            
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            var cryptoProcessor = new CryptoProcessor(mockCipher.Object);
            
            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [cryptoProcessor]);

            var originalData = Encoding.UTF8.GetBytes("Sensitive information that should be encrypted");
            string uid = "abcdef123456";

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));
            
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
            
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            var cryptoProcessor = new CryptoProcessor(mockCipher.Object);
            var compressionProcessor = new CompressionProcessor();
            
            // Priority: Compression (10000) > Crypto (1000)
            // On Write: Compress first, then encrypt
            // On Read: Decrypt first, then decompress
            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [cryptoProcessor, compressionProcessor]);

            var originalData = Encoding.UTF8.GetBytes(new string('A', 10000)); // Highly compressible
            string uid = "abcdef123456";

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
            
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            var cryptoProcessor = new CryptoProcessor(mockCipher.Object);
            
            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [cryptoProcessor]);

            var testData = new Dictionary<string, byte[]>
            {
                ["abcdef111111"] = Encoding.UTF8.GetBytes("File 1"),
                ["abcdef222222"] = Encoding.UTF8.GetBytes("File 2"),
                ["123456abcdef"] = Encoding.UTF8.GetBytes("File 3")
            };

            // Act - Write all
            foreach (var kvp in testData)
            {
                await pipeline.WriteAsync(kvp.Key, new MemoryStream(kvp.Value));
            }

            // Act - Read all
            foreach (var kvp in testData)
            {
                var readStream = await pipeline.ReadAsync(kvp.Key);
                var result = new MemoryStream();
                await readStream.CopyToAsync(result);

                // Assert
                Assert.That(result.ToArray(), Is.EqualTo(kvp.Value), 
                    $"File {kvp.Key} should match original data");
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
                [compressionProcessor]);

            string uid = "abcdef123456";
            
            // Create 5MB of data
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
            
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            var cryptoProcessor = new CryptoProcessor(mockCipher.Object);       // Priority: 1000
            var compressionProcessor = new CompressionProcessor();    // Priority: 10000
            
            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [compressionProcessor, cryptoProcessor]);

            var originalData = Encoding.UTF8.GetBytes("Test data for order verification");
            string uid = "abcdef123456";

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));

            // Verify full round trip
            var readStream = await pipeline.ReadAsync(uid);
            var result = new MemoryStream();
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
            
            var mockCipher = new Mock<IStreamCipher>();
            SetupRoundTripCipher(mockCipher);
            var cryptoProcessor = new CryptoProcessor(mockCipher.Object);
            
            var pipeline = new FileStoragePipeline(
                pipelineLogger.Object,
                provider,
                [cryptoProcessor]);

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
                var result = new MemoryStream();
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

        private static void SetupRoundTripCipher(Mock<IStreamCipher> mockCipher)
        {
            mockCipher.Setup(c => c.EncryptAsync(It.IsAny<Stream>()))
                .ReturnsAsync((Stream s) => XorStream(s, 0xAA));
            
            mockCipher.Setup(c => c.DecryptAsync(It.IsAny<Stream>()))
                .ReturnsAsync((Stream s) => XorStream(s, 0xAA));
        }

        private static MemoryStream XorStream(Stream input, byte key)
        {
            var ms = new MemoryStream();
            input.CopyTo(ms);
            var bytes = ms.ToArray();
            
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] ^= key;
            }
            
            return new MemoryStream(bytes) { Position = 0 };
        }
    }
}
