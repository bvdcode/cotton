// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3.Model;
using Cotton.Storage.Streams;
using Moq;

namespace Cotton.Storage.Tests.Streams
{
    public class S3ResponseStreamTests
    {
        [Test]
        public async Task CopyToAsync_UsesInnerAsynchronousReadPath()
        {
            byte[] content = [1, 2, 3, 4, 5];
            int position = 0;
            int asyncReadCount = 0;
            Mock<Stream> innerStream = new();
            innerStream.SetupGet(x => x.CanRead).Returns(true);
            innerStream
                .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Throws(new InvalidOperationException("Synchronous reads are not allowed."));
            innerStream
                .Setup(x => x.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                .Returns((Memory<byte> buffer, CancellationToken cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    asyncReadCount++;
                    int count = Math.Min(buffer.Length, content.Length - position);
                    content.AsMemory(position, count).CopyTo(buffer);
                    position += count;
                    return ValueTask.FromResult(count);
                });
            GetObjectResponse response = new()
            {
                ResponseStream = innerStream.Object
            };

            await using S3ResponseStream stream = new(response);
            using MemoryStream destination = new();

            await stream.CopyToAsync(destination);

            Assert.That(destination.ToArray(), Is.EqualTo(content));
            Assert.That(asyncReadCount, Is.GreaterThan(0));
        }
    }
}
