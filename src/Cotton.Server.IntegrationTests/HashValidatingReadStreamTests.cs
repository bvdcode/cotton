// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    public class HashValidatingReadStreamTests
    {
        [Test]
        public async Task CorrectContent_IsReadAndValidated()
        {
            byte[] content = RandomNumberGenerator.GetBytes(1024 * 1024);
            using MemoryStream inner = new(content);
            using HashValidatingReadStream stream = new(inner, content.Length, SHA256.HashData(content));
            using MemoryStream destination = new();

            await stream.CopyToAsync(destination);
            stream.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(stream.IsValidated, Is.True);
                Assert.That(destination.ToArray(), Is.EqualTo(content));
                Assert.That(inner.CanRead, Is.True);
            });
            Assert.DoesNotThrow(stream.EnsureValidated);
        }

        [Test]
        public void HashMismatch_ThrowsBeforeTheFinalBytesArePublished()
        {
            byte[] content = RandomNumberGenerator.GetBytes(128 * 1024);
            byte[] differentContent = content.ToArray();
            differentContent[^1] ^= 0xff;
            using MemoryStream inner = new(content);
            using HashValidatingReadStream stream = new(inner, content.Length, SHA256.HashData(differentContent));
            using MemoryStream destination = new();

            InvalidDataException? exception = Assert.ThrowsAsync<InvalidDataException>(
                async () => await stream.CopyToAsync(destination));

            Assert.Multiple(() =>
            {
                Assert.That(exception?.Message, Does.Contain("Hash mismatch"));
                Assert.That(stream.IsValidated, Is.False);
                Assert.That(destination.Length, Is.LessThan(content.Length));
            });
        }

        [Test]
        public void ShortContent_ThrowsUnexpectedLength()
        {
            byte[] content = RandomNumberGenerator.GetBytes(64 * 1024);
            using MemoryStream inner = new(content);
            using HashValidatingReadStream stream = new(inner, content.Length + 1, SHA256.HashData(content));

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await stream.CopyToAsync(Stream.Null));

            Assert.That(exception?.Message, Is.EqualTo("Unexpected stream length."));
        }

        [Test]
        public void LongContent_ThrowsUnexpectedLength()
        {
            byte[] content = RandomNumberGenerator.GetBytes(64 * 1024);
            using MemoryStream inner = new(content);
            using HashValidatingReadStream stream = new(inner, content.Length - 1, SHA256.HashData(content));

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await stream.CopyToAsync(Stream.Null));

            Assert.That(exception?.Message, Is.EqualTo("Unexpected stream length."));
        }

        [Test]
        public async Task EmptyContent_IsValidated()
        {
            using MemoryStream inner = new([]);
            using HashValidatingReadStream stream = new(inner, 0, SHA256.HashData([]));

            await stream.CopyToAsync(Stream.Null);

            Assert.That(stream.IsValidated, Is.True);
        }

        [Test]
        public void Cancellation_StopsReadingAndLeavesInnerStreamOpen()
        {
            byte[] content = RandomNumberGenerator.GetBytes(64 * 1024);
            using MemoryStream inner = new(content);
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            using HashValidatingReadStream stream = new(
                inner,
                content.Length,
                SHA256.HashData(content),
                cancellation.Token);

            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await stream.CopyToAsync(Stream.Null));
            Assert.Multiple(() =>
            {
                Assert.That(stream.IsValidated, Is.False);
                Assert.That(inner.CanRead, Is.True);
            });
        }
    }
}
