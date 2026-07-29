// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Cotton.Server.IntegrationTests;

public class FileStoragePipelineAdmissionTests
{
    [Test]
    [NonParallelizable]
    public async Task CancelledWriteWaiterDoesNotRunAfterCapacityIsReleased()
    {
        int parallelism = Environment.ProcessorCount;
        InMemoryBackend backend = new();
        StaticBackendProvider provider = new(backend);
        BlockingWriteProcessor processor = new(parallelism);
        FileStoragePipeline pipeline = new(
            NullLogger<FileStoragePipeline>.Instance,
            provider,
            [processor]);
        Task[] blockers = Enumerable.Range(0, parallelism)
            .Select(index => pipeline.WriteAsync(
                $"blocker-{index}",
                new MemoryStream([1])))
            .ToArray();
        Task? followerWrite = null;

        try
        {
            await processor.AllBlockersEntered.WaitAsync(TimeSpan.FromSeconds(5));

            using CancellationTokenSource cancellation = new();
            Task cancelledWrite = pipeline.WriteAsync(
                "cancelled",
                new MemoryStream([2]),
                cancellationToken: cancellation.Token);
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                async () => await cancelledWrite.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.That(processor.HasEntered("cancelled"), Is.False);

            followerWrite = pipeline.WriteAsync(
                "follower",
                new MemoryStream([3]));
            Assert.That(processor.HasEntered("follower"), Is.False);
        }
        finally
        {
            processor.ReleaseBlockers();
        }

        await Task.WhenAll(blockers).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(followerWrite, Is.Not.Null);
        await followerWrite!.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(processor.HasEntered("cancelled"), Is.False);
            Assert.That(processor.HasEntered("follower"), Is.True);
        });
    }

    private sealed class BlockingWriteProcessor(int expectedBlockers) : IStorageProcessor
    {
        private readonly ConcurrentDictionary<string, byte> _entered =
            new(StringComparer.Ordinal);
        private readonly TaskCompletionSource _allBlockersEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseBlockers =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _blockersEntered;

        public int Priority => 100;

        public Task AllBlockersEntered => _allBlockersEntered.Task;

        public bool HasEntered(string uid) => _entered.ContainsKey(uid);

        public void ReleaseBlockers() => _releaseBlockers.TrySetResult();

        public Task<Stream> ReadAsync(
            string uid,
            Stream stream,
            PipelineContext? context = null)
        {
            return Task.FromResult(stream);
        }

        public async Task<Stream> WriteAsync(
            string uid,
            Stream stream,
            PipelineContext? context = null)
        {
            _entered.TryAdd(uid, 0);
            if (uid.StartsWith("blocker-", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _blockersEntered) == expectedBlockers)
                {
                    _allBlockersEntered.TrySetResult();
                }

                await _releaseBlockers.Task;
            }

            return stream;
        }
    }

    private sealed class StaticBackendProvider(IStorageBackend backend) : IStorageBackendProvider
    {
        public IStorageBackend GetBackend() => backend;
    }

    private sealed class InMemoryBackend : IStorageBackend
    {
        private readonly ConcurrentDictionary<string, byte[]> _storage =
            new(StringComparer.Ordinal);

        public Task<bool> DeleteAsync(string uid) =>
            Task.FromResult(_storage.TryRemove(uid, out _));

        public Task<bool> ExistsAsync(string uid) =>
            Task.FromResult(_storage.ContainsKey(uid));

        public Task<long> GetSizeAsync(string uid) =>
            Task.FromResult(_storage.TryGetValue(uid, out byte[]? data) ? data.Length : 0L);

        public Task<Stream> ReadAsync(string uid)
        {
            if (!_storage.TryGetValue(uid, out byte[]? data))
            {
                throw new FileNotFoundException("Blob not found.", uid);
            }

            return Task.FromResult<Stream>(new MemoryStream(data, writable: false));
        }

        public async Task WriteAsync(
            string uid,
            Stream stream)
        {
            using MemoryStream destination = new();
            await stream.CopyToAsync(destination);
            _storage[uid] = destination.ToArray();
        }

        public async IAsyncEnumerable<string> ListAllKeysAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (string key in _storage.Keys)
            {
                ct.ThrowIfCancellationRequested();
                yield return key;
            }

            await Task.CompletedTask;
        }

        public void CleanupTempFiles(TimeSpan ttl)
        {
        }
    }
}
