// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Amazon.Runtime;
using Cotton.Storage.Backends;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Helpers;
using Cotton.Storage.Tests.Helpers;
using Moq;
using System.Security.Cryptography;

namespace Cotton.Storage.Tests.Backends
{
    public class S3BufferedWriteSdkTests
    {
        [TestCase(0)]
        [TestCase(4 * 1024 * 1024)]
        [TestCase(16 * 1024 * 1024 + 4096)]
        public async Task Write_RealSdk_RetriesSameSignedBodyWithoutChunkEncoding(int size)
        {
            byte[] data = RandomNumberGenerator.GetBytes(size);
            using S3RetryTestHandler handler = new(SHA256.HashData(data), data.Length);
            using HttpClient http = new(handler);
            AmazonS3Config config = S3CompatibilityFactory.BuildConfig("https://s3.example.test", "us-east-1", maxErrorRetry: 1);
            config.HttpClientFactory = new S3TestHttpClientFactory(http);
            using AmazonS3Client client = new(new BasicAWSCredentials("test-access", "test-secret"), config);
            Mock<IS3Provider> provider = new();
            provider.Setup(x => x.GetS3Client()).Returns(client);
            provider.Setup(x => x.GetBucketName()).Returns("test-bucket");
            S3StorageBackend backend = new(provider.Object);
            using MemoryStream source = new(data);

            Assert.That(await backend.WriteAsync("abcdef123456", source, overwrite: true), Is.EqualTo(size));
            Assert.That(handler.Attempts, Is.EqualTo(2));
        }
    }
}
