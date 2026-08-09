// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Moq;
using System.Net;

namespace Cotton.Storage.Tests.Backends
{
    public class S3StorageBackendCompatibilityTests
    {
        [Test]
        public async Task ReadAsync_InvalidFullRange_ReturnsEmptyStream()
        {
            AmazonS3Exception exception = new("Invalid range")
            {
                ErrorCode = "InvalidRange",
                StatusCode = HttpStatusCode.RequestedRangeNotSatisfiable,
            };
            GetObjectRequest? capturedRequest = null;
            Mock<IAmazonS3> s3 = new();
            s3.Setup(x => x.GetObjectAsync(
                    It.IsAny<GetObjectRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<GetObjectRequest, CancellationToken>((request, _) => capturedRequest = request)
                .ThrowsAsync(exception);
            Mock<IS3Provider> provider = new();
            provider.Setup(x => x.GetS3Client()).Returns(s3.Object);
            provider.Setup(x => x.GetBucketName()).Returns("test-bucket");
            S3StorageBackend backend = new(provider.Object);

            await using Stream stream = await backend.ReadAsync("abcdef");

            Assert.Multiple(() =>
            {
                Assert.That(stream.ReadByte(), Is.EqualTo(-1));
                Assert.That(capturedRequest, Is.Not.Null);
                Assert.That(
                    capturedRequest!.ByteRange.FormattedByteRange,
                    Is.EqualTo("bytes=0-"));
            });
        }
    }
}
