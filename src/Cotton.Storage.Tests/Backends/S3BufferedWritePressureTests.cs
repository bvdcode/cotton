// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Storage.Backends;
using Moq;

namespace Cotton.Storage.Tests.Backends
{
    [NonParallelizable]
    public class S3BufferedWritePressureTests
    {
        private const int MiB = 1024 * 1024;

        [TestCase(4 * MiB, 16)]
        [TestCase((16 * MiB) + 1, 5)]
        [TestCase(32 * MiB, 4)]
        public async Task Write_PoolFull_WaitsAcrossBackendInstancesAndCancellationReleasesPartialBuffer(int size, int activeWrites)
        {
            Mock<IAmazonS3> client = new(MockBehavior.Strict);
            TaskCompletionSource full = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int started = 0;
            client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .Returns<PutObjectRequest, CancellationToken>(async (request, ct) =>
                {
                    Assert.That(request.InputStream.Length, Is.EqualTo(size));
                    if (Interlocked.Increment(ref started) == activeWrites)
                    {
                        full.SetResult();
                    }
                    await release.Task.WaitAsync(ct);
                    return new PutObjectResponse();
                });
            byte[] content = new byte[size];
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(20));
            List<Task<long>> writes = [];
            try
            {
                for (int index = 0; index < activeWrites; index++)
                {
                    S3StorageBackend backend = S3BufferedWriteTests.CreateBackend(client);
                    writes.Add(backend.WriteAsync("abcdef123456", new MemoryStream(content), overwrite: true, deadline.Token));
                }
                await full.Task.WaitAsync(deadline.Token);
                using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                S3StorageBackend waitingBackend = S3BufferedWriteTests.CreateBackend(client);
                Task<long> waiting = waitingBackend.WriteAsync("abcdef123456", new MemoryStream(content), overwrite: true, cancel.Token);
                Task<long> next = waitingBackend.WriteAsync("abcdef123456", new MemoryStream(content), overwrite: true, deadline.Token);
                writes.Add(next);
                Assert.Multiple(() =>
                {
                    Assert.That(waiting.IsCompleted, Is.False);
                    Assert.That(next.IsCompleted, Is.False);
                    Assert.That(started, Is.EqualTo(activeWrites));
                });
                await cancel.CancelAsync();
                await Assert.CatchAsync<OperationCanceledException>(async () => await waiting);
                Assert.That(next.IsCompleted, Is.False);
            }
            finally
            {
                release.TrySetResult();
                await Task.WhenAll(writes).WaitAsync(deadline.Token);
            }
            Assert.That(started, Is.EqualTo(activeWrites + 1));
        }

        [Test]
        public async Task Write_CancelledDuringPut_ReturnsAllBlocks()
        {
            Mock<IAmazonS3> client = new(MockBehavior.Strict);
            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Stream? body = null;
            client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .Returns<PutObjectRequest, CancellationToken>(async (request, ct) =>
                {
                    body = request.InputStream;
                    started.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                    return new PutObjectResponse();
                });
            S3StorageBackend backend = S3BufferedWriteTests.CreateBackend(client);
            using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));
            using MemoryStream source = new(new byte[17 * MiB]);
            Task<long> write = backend.WriteAsync("abcdef123456", source, overwrite: true, cancellation.Token);
            await started.Task.WaitAsync(cancellation.Token);
            await cancellation.CancelAsync();

            await Assert.CatchAsync<OperationCanceledException>(async () => await write);
            Assert.That(body!.CanRead, Is.False);
            client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PutObjectResponse());
            Assert.That(await backend.WriteAsync("abcdef123456", source, overwrite: true), Is.EqualTo(source.Length));
        }
        [Test]
        public async Task Write_ObjectLargerThanBudget_FailsWithoutDeadlockAndReturnsEntireBudget()
        {
            Mock<IAmazonS3> client = new(MockBehavior.Strict);
            Mock<Stream> source = new();
            long remaining = (128 * MiB) + 1;
            source.Setup(x => x.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                .Returns<Memory<byte>, CancellationToken>((buffer, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    int count = (int)Math.Min(remaining, buffer.Length);
                    buffer.Span[..count].Fill(63);
                    remaining -= count;
                    return ValueTask.FromResult(count);
                });
            S3StorageBackend backend = S3BufferedWriteTests.CreateBackend(client);
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));

            await Assert.ThrowsAsync<IOException>(() => backend.WriteAsync("abcdef123456", source.Object, overwrite: true, deadline.Token));
            client.VerifyNoOtherCalls();
            remaining = 128 * MiB;
            client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                .Returns<PutObjectRequest, CancellationToken>((request, _) =>
                {
                    Assert.That(request.InputStream.Length, Is.EqualTo(128 * MiB));
                    request.InputStream.Seek(-1, SeekOrigin.End);
                    Assert.That(request.InputStream.ReadByte(), Is.EqualTo(63));
                    return Task.FromResult(new PutObjectResponse());
                });
            Assert.That(await backend.WriteAsync("abcdef123456", source.Object, overwrite: true, deadline.Token), Is.EqualTo(128 * MiB));
        }
    }
}
