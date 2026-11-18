// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;
using Moq;
using System.Net;
using System.Security.Cryptography;

namespace Cotton.Storage.Tests.Backends
{
    [NonParallelizable]
    public class S3BufferedWriteTests
    {
        private const int MiB = 1024 * 1024;

        internal static S3StorageBackend CreateBackend(Mock<IAmazonS3> client)
        {
            Mock<IS3Provider> provider = new();
            provider.Setup(x => x.GetS3Client()).Returns(client.Object);
            provider.Setup(x => x.GetBucketName()).Returns("test-bucket");
            return new S3StorageBackend(provider.Object);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4 * MiB)]
        [TestCase(8 * MiB)]
        [TestCase(8 * MiB + 1)]
        [TestCase(16 * MiB + 4096)]
        public async Task Write_NonSeekableSource_UsesOneRewindableSignedPut(int size)
        {
            byte[] content = RandomNumberGenerator.GetBytes(size);
            byte[] expectedHash = SHA256.HashData(content);
            Mock<IAmazonS3> client = new(MockBehavior.Strict);
            Stream? body = null;
            client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .Returns<PutObjectRequest, CancellationToken>(async (request, ct) =>
                {
                    body = request.InputStream;
                    Assert.Multiple(() =>
                    {
                        Assert.That(request.FilePath, Is.Null.Or.Empty);
                        Assert.That(request.Headers.ContentLength, Is.EqualTo(size));
                        Assert.That(request.UseChunkEncoding, Is.False);
                        Assert.That(request.DisablePayloadSigning, Is.Null);
                        Assert.That(body.CanSeek, Is.True);
                        Assert.That(body.Position, Is.Zero);
                    });
                    Assert.That(await SHA256.HashDataAsync(body, ct), Is.EqualTo(expectedHash));
                    body.Seek(0, SeekOrigin.Begin);
                    Assert.That(await SHA256.HashDataAsync(body, ct), Is.EqualTo(expectedHash));
                    if (size > 8 * MiB)
                    {
                        body.Seek(8 * MiB - 3, SeekOrigin.Begin);
                        byte[] crossing = new byte[Math.Min(7, size - (8 * MiB - 3))];
                        await body.ReadExactlyAsync(crossing, ct);
                        Assert.That(crossing, Is.EqualTo(content.AsSpan(8 * MiB - 3, crossing.Length).ToArray()));
                    }
                    return new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK };
                });
            S3StorageBackend backend = CreateBackend(client);
            using MemoryStream input = new(content);
            Mock<Stream> source = new();
            source.Setup(x => x.CanSeek).Returns(false);
            source.Setup(x => x.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                .Returns<Memory<byte>, CancellationToken>((buffer, ct) => input.ReadAsync(buffer[..Math.Min(buffer.Length, 65537)], ct));

            long length = await backend.WriteAsync("abcdef123456", source.Object, overwrite: true);

            Assert.Multiple(() =>
            {
                Assert.That(length, Is.EqualTo(size));
                Assert.That(body!.CanRead, Is.False);
                Assert.That(input.CanRead, Is.True);
            });
            client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            client.VerifyNoOtherCalls();
        }

        [Test]
        public async Task Write_SeekableSource_RewindsBeforeBuffering()
        {
            byte[] content = [1, 2, 3, 4, 5];
            Mock<IAmazonS3> client = new(MockBehavior.Strict);
            client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .Returns<PutObjectRequest, CancellationToken>(async (request, ct) =>
                {
                    Assert.That(await SHA256.HashDataAsync(request.InputStream, ct), Is.EqualTo(SHA256.HashData(content)));
                    return new PutObjectResponse();
                });
            S3StorageBackend backend = CreateBackend(client);
            using MemoryStream source = new(content) { Position = 3 };

            Assert.That(await backend.WriteAsync("abcdef123456", source, overwrite: true), Is.EqualTo(content.Length));
        }

        [Test]
        public async Task Write_FailedPut_DisposesBufferedBodyAndAllowsNextWrite()
        {
            Mock<IAmazonS3> client = new();
            Stream? body = null;
            IOException failure = new("Connection lost");
            client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .Callback<PutObjectRequest, CancellationToken>((request, _) => body = request.InputStream)
                .ThrowsAsync(failure);
            S3StorageBackend backend = CreateBackend(client);
            using MemoryStream source = new(new byte[17 * MiB]);

            Assert.That(await Assert.ThrowsAsync<IOException>(() => backend.WriteAsync("abcdef123456", source, overwrite: true)), Is.SameAs(failure));
            Assert.That(body!.CanRead, Is.False);
            client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PutObjectResponse());
            Assert.That(await backend.WriteAsync("abcdef123456", source, overwrite: true), Is.EqualTo(source.Length));
        }

        [Test]
        public async Task Write_SourceFailure_ReturnsBlocksWithoutSendingPartialObject()
        {
            Mock<IAmazonS3> client = new(MockBehavior.Strict);
            Mock<Stream> source = new();
            int reads = 0;
            source.Setup(x => x.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                .Returns<Memory<byte>, CancellationToken>((buffer, _) =>
                {
                    if (++reads > 2)
                    {
                        throw new IOException("Source failed");
                    }
                    buffer.Span.Clear();
                    return ValueTask.FromResult(buffer.Length);
                });
            S3StorageBackend backend = CreateBackend(client);

            await Assert.ThrowsAsync<IOException>(() => backend.WriteAsync("abcdef123456", source.Object, overwrite: true));
            client.VerifyNoOtherCalls();
            client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PutObjectResponse());
            using MemoryStream next = new(new byte[1]);
            Assert.That(await backend.WriteAsync("abcdef123456", next, overwrite: true), Is.EqualTo(1));
        }
    }
}
