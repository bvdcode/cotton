// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using EasyExtensions.Crypto;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cotton.Storage.Tests.Backends
{
    [TestFixture]
    public class S3StorageBackendTests
    {
        private S3StorageBackend _backend = null!;
        private AmazonS3Client _s3Client = null!;
        private string _bucketName = null!;
        private readonly List<string> _createdKeys = [];
        private S3TestConfig? _testConfig;

        private static string NewUid() => Guid.NewGuid().ToString("N")[..12];

        private class S3TestConfig
        {
            public string AccessKey { get; set; } = string.Empty;
            public string SecretKey { get; set; } = string.Empty;
            public string Endpoint { get; set; } = string.Empty;
            public string Bucket { get; set; } = string.Empty;
            public string Region { get; set; } = string.Empty;
        }

        [SetUp]
        public void Setup()
        {
            var configPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "s3-test-config.json");
            if (!File.Exists(configPath))
            {
                Assert.Ignore($"S3 test config not found at {configPath}. Copy s3-test-config.json.example to s3-test-config.json and fill in your credentials.");
            }

            var configJson = File.ReadAllText(configPath);
            _testConfig = JsonSerializer.Deserialize<S3TestConfig>(configJson);

            if (_testConfig == null || string.IsNullOrEmpty(_testConfig.AccessKey))
            {
                Assert.Ignore("S3 test config is invalid or empty.");
            }

            _bucketName = _testConfig.Bucket;

            var config = new AmazonS3Config
            {
                ServiceURL = _testConfig.Endpoint,
                ForcePathStyle = true,
                UseHttp = false,
                MaxErrorRetry = 3,
                Timeout = TimeSpan.FromMinutes(2),
                AuthenticationRegion = _testConfig.Region,
            };

            _s3Client = new AmazonS3Client(_testConfig.AccessKey, _testConfig.SecretKey, config);

            var s3Provider = new TestS3Provider(_s3Client, _bucketName);
            _backend = new S3StorageBackend(s3Provider);
            _createdKeys.Clear();
        }

        [TearDown]
        public async Task TearDown()
        {
            foreach (var uid in _createdKeys)
            {
                try
                {
                    var (p1, p2, fileName) = Storage.Helpers.StorageKeyHelper.GetSegments(uid);
                    var key = $"{p1}/{p2}/{fileName}";
                    await _s3Client.DeleteObjectAsync(_bucketName, key);
                }
                catch
                {
                    // Best effort cleanup
                }
            }

            _s3Client?.Dispose();
        }

        private class TestS3Provider(IAmazonS3 s3Client, string bucketName) : IS3Provider
        {
            public string GetBucketName() => bucketName;
            public IAmazonS3 GetS3Client() => s3Client;
        }

        [Test]
        public async Task S3Backend_WriteAndRead_ReturnsOriginalData()
        {
            // Arrange
            string uid = NewUid();
            _createdKeys.Add(uid);
            var originalData = Encoding.UTF8.GetBytes("Test content for S3 backend");

            // Act
            await _backend.WriteAsync(uid, new MemoryStream(originalData));
            await using var readStream = await _backend.ReadAsync(uid);

            // Assert
            using var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public async Task S3Backend_FullPipeline_WithEncryptionAndCompression_RoundTrip()
        {
            // Arrange
            string uid = NewUid();
            _createdKeys.Add(uid);

            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cipher = new AesGcmStreamCipher(key, keyId: 1, threads: 2);

            var processors = new IStorageProcessor[]
            {
                new CryptoProcessor(cipher),
                new CompressionProcessor()
            };

            var backendProvider = new TestBackendProvider(_backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();
            var pipeline = new FileStoragePipeline(logger.Object, backendProvider, processors);

            var originalData = new byte[1024 * 1024]; // 1 MB
            RandomNumberGenerator.Fill(originalData);

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));
            await using var readStream = await pipeline.ReadAsync(uid);

            // Assert
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));

            cipher.Dispose();
        }

        [Test]
        public async Task S3Backend_FullPipeline_NonSeekableStream_Success()
        {
            // Arrange
            string uid = NewUid();
            _createdKeys.Add(uid);

            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cipher = new AesGcmStreamCipher(key, keyId: 1, threads: 2);

            var processors = new IStorageProcessor[]
            {
                new CryptoProcessor(cipher),
                new CompressionProcessor()
            };

            var backendProvider = new TestBackendProvider(_backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();
            var pipeline = new FileStoragePipeline(logger.Object, backendProvider, processors);

            var originalData = Encoding.UTF8.GetBytes("Non-seekable stream test data");
            var nonSeekableStream = new NonSeekableMemoryStream(originalData);

            // Act
            await pipeline.WriteAsync(uid, nonSeekableStream);
            await using var readStream = await pipeline.ReadAsync(uid);

            // Assert
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));

            cipher.Dispose();
        }

        [Test]
        public async Task S3Backend_FullPipeline_LargeFile_Success()
        {
            // Arrange
            string uid = NewUid();
            _createdKeys.Add(uid);

            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cipher = new AesGcmStreamCipher(key, keyId: 1, threads: 2);

            var processors = new IStorageProcessor[]
            {
                new CryptoProcessor(cipher),
                new CompressionProcessor()
            };

            var backendProvider = new TestBackendProvider(_backend);
            var logger = new Mock<ILogger<FileStoragePipeline>>();
            var pipeline = new FileStoragePipeline(logger.Object, backendProvider, processors);

            var originalData = new byte[5 * 1024 * 1024]; // 5 MB
            RandomNumberGenerator.Fill(originalData);

            // Act
            await pipeline.WriteAsync(uid, new MemoryStream(originalData));
            await using var readStream = await pipeline.ReadAsync(uid);

            // Assert
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(originalData));

            cipher.Dispose();
        }

        [Test]
        public async Task S3Backend_Delete_AfterWrite_ReturnsTrue()
        {
            // Arrange
            string uid = NewUid();
            _createdKeys.Add(uid);
            var data = Encoding.UTF8.GetBytes("Test content");
            await _backend.WriteAsync(uid, new MemoryStream(data));

            // Act
            bool result = await _backend.DeleteAsync(uid);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task S3Backend_Delete_NonExistent_ReturnsFalseOrTrue()
        {
            // Arrange
            string uid = NewUid();

            // Act
            bool result = await _backend.DeleteAsync(uid);

            // Assert - S3 returns 204 NoContent even for non-existent keys
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task S3Backend_Write_LargeFile_Success()
        {
            // Arrange
            string uid = NewUid();
            _createdKeys.Add(uid);
            var data = new byte[32 * 1024 * 1024]; // 32 MB
            RandomNumberGenerator.Fill(data);

            // Act
            await _backend.WriteAsync(uid, new MemoryStream(data));
            await using var readStream = await _backend.ReadAsync(uid);

            // Assert
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public async Task S3Backend_Write_NonSeekableStream_Success()
        {
            // Arrange
            string uid = NewUid();
            _createdKeys.Add(uid);
            var data = Encoding.UTF8.GetBytes("Non-seekable stream content");
            var nonSeekableStream = new NonSeekableMemoryStream(data);

            // Act
            await _backend.WriteAsync(uid, nonSeekableStream);
            await using var readStream = await _backend.ReadAsync(uid);

            // Assert
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public async Task S3Backend_Write_StreamNotAtPositionZero_Success()
        {
            // Arrange
            string uid = NewUid();
            _createdKeys.Add(uid);
            var data = Encoding.UTF8.GetBytes("0123456789");
            var stream = new MemoryStream(data) { Position = 5 };

            // Act
            await _backend.WriteAsync(uid, stream);
            await using var readStream = await _backend.ReadAsync(uid);

            // Assert
            var result = new MemoryStream();
            await readStream.CopyToAsync(result);
            Assert.That(result.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public async Task S3Backend_MultipleWrites_DifferentUids_Success()
        {
            // Arrange
            var uids = new[] { NewUid(), NewUid(), NewUid() };
            var dataMap = new Dictionary<string, byte[]>();

            foreach (var uid in uids)
            {
                _createdKeys.Add(uid);
                var data = Encoding.UTF8.GetBytes($"Content for {uid}");
                dataMap[uid] = data;
                await _backend.WriteAsync(uid, new MemoryStream(data));
            }

            // Act & Assert
            foreach (var uid in uids)
            {
                await using var readStream = await _backend.ReadAsync(uid);
                var result = new MemoryStream();
                await readStream.CopyToAsync(result);
                Assert.That(result.ToArray(), Is.EqualTo(dataMap[uid]));
            }
        }

        [Test]
        public async Task S3Backend_ParallelWrites_DifferentUids_AllSucceed()
        {
            // Arrange
            var tasks = new List<Task>();
            var uids = Enumerable.Range(0, 5).Select(_ => NewUid()).ToArray();

            foreach (var uid in uids)
            {
                _createdKeys.Add(uid);
            }

            // Act
            foreach (var uid in uids)
            {
                var data = Encoding.UTF8.GetBytes($"Parallel content {uid}");
                tasks.Add(_backend.WriteAsync(uid, new MemoryStream(data)));
            }

            // Assert
            Assert.DoesNotThrowAsync(() => Task.WhenAll(tasks));

            foreach (var uid in uids)
            {
                Assert.DoesNotThrowAsync(async () =>
                {
                    await using var stream = await _backend.ReadAsync(uid);
                    var result = new MemoryStream();
                    await stream.CopyToAsync(result);
                    Assert.That(result.Length, Is.GreaterThan(0));
                });
            }
        }

        private class NonSeekableMemoryStream(byte[] buffer) : MemoryStream(buffer)
        {
            public override bool CanSeek => false;
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override long Position
            {
                get => base.Position;
                set => throw new NotSupportedException();
            }
        }

        private class TestBackendProvider(IStorageBackend backend) : IStorageBackendProvider
        {
            public IStorageBackend GetBackend() => backend;
        }
    }
}
