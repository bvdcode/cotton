// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Cryptography;

namespace Cotton.Storage.Tests.Pipelines
{
    public class StoragePipelineLifetimeTests
    {
        [Test]
        public async Task FailedWrite_StopsCompressionAndReleasesInput()
        {
            Mock<IStorageBackend> backend = new();
            backend.Setup(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), false, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Write failed."));
            FileStoragePipeline pipeline = CreatePipeline(backend.Object);
            using MemoryStream input = new(RandomNumberGenerator.GetBytes(8 * 1024 * 1024));

            await Assert.ThatAsync(
                () => pipeline.WriteAsync("chunk", input).WaitAsync(TimeSpan.FromSeconds(5)),
                Throws.InstanceOf<IOException>());

            Assert.That(input.CanRead, Is.False);
        }

        [Test]
        public async Task CanceledWrite_StopsBackendAndReleasesAdmission()
        {
            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Mock<IStorageBackend> backend = new();
            int calls = 0;
            backend.Setup(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), false, It.IsAny<CancellationToken>()))
                .Returns(async (string uid, Stream stream, bool overwrite, CancellationToken token) =>
                {
                    if (Interlocked.Increment(ref calls) == 1)
                    {
                        started.SetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    }
                    await stream.CopyToAsync(Stream.Null, token);
                    return 1L;
                });
            FileStoragePipeline pipeline = CreatePipeline(backend.Object);
            using CancellationTokenSource cancellation = new();
            using MemoryStream first = new([1, 2, 3]);
            Task<long> write = pipeline.WriteAsync("first", first, cancellationToken: cancellation.Token);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellation.CancelAsync();

            await Assert.ThatAsync(() => write.WaitAsync(TimeSpan.FromSeconds(5)), Throws.InstanceOf<OperationCanceledException>());
            using MemoryStream second = new([4, 5, 6]);
            Assert.That(await pipeline.WriteAsync("second", second).WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(1));
            Assert.That(first.CanRead, Is.False);
        }

        [Test]
        public async Task FailedReadProcessor_ReleasesBackendStream()
        {
            using MemoryStream source = new([1]);
            Mock<IStorageBackend> backend = new();
            backend.Setup(x => x.ReadAsync("chunk", It.IsAny<CancellationToken>())).ReturnsAsync(source);
            Mock<IStorageProcessor> processor = new();
            processor.Setup(x => x.ReadAsync("chunk", source, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidDataException());
            FileStoragePipeline pipeline = CreatePipeline(backend.Object, processor.Object);

            await Assert.ThatAsync(() => pipeline.ReadAsync("chunk"), Throws.InstanceOf<InvalidDataException>());

            Assert.That(source.CanRead, Is.False);
        }

        private static FileStoragePipeline CreatePipeline(IStorageBackend backend, IStorageProcessor? processor = null)
        {
            Mock<IStorageBackendProvider> provider = new();
            provider.Setup(x => x.GetBackend()).Returns(backend);
            return new(NullLogger<FileStoragePipeline>.Instance, provider.Object,
                [processor ?? new CompressionProcessor()], new StorageWriteAdmissionGate(1));
        }
    }
}
