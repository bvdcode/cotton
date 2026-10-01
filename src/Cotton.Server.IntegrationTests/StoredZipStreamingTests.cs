// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Options;

namespace Cotton.Server.IntegrationTests
{
    public class StoredZipStreamingTests
    {
        [Test]
        public async Task StreamingWriter_WritesZip64EntryCount_WithExactCalculatedLength()
        {
            StoredZipArchiveWriter writer = new(Options.Create(new ResourceConcurrencyOptions()));
            StoredZipLengthCalculator expected = new();
            const int count = ushort.MaxValue + 1;
            for (int i = 0; i < count; i++)
            {
                expected.Add(new ArchiveDownloadDirectoryEntry($"папка-{i}/"));
            }
            using MemoryStream destination = new();
            await writer.WriteAsync(destination, EnumerateEntries());
            Assert.That(destination.Length, Is.EqualTo(expected.TotalLength));
            destination.Position = 0;
            using ZipArchive zip = new(destination, ZipArchiveMode.Read);
            Assert.That(zip.Entries.Count, Is.EqualTo(count));

            async IAsyncEnumerable<StoredZipSourceEntry> EnumerateEntries()
            {
                await Task.CompletedTask;
                for (int i = 0; i < count; i++)
                {
                    yield return new StoredZipSourceEntry($"папка-{i}/", 0, true,
                        _ => ValueTask.FromResult<Stream>(Stream.Null));
                }
            }
        }

        [Test]
        public async Task StreamingWriter_ConsumesNextEntryOnlyAfterWritingCurrentBody()
        {
            StoredZipArchiveWriter writer = new(Options.Create(new ResourceConcurrencyOptions()));
            using MemoryStream destination = new();
            await writer.WriteAsync(destination, EnumerateEntries());

            async IAsyncEnumerable<StoredZipSourceEntry> EnumerateEntries()
            {
                await Task.CompletedTask;
                yield return new StoredZipSourceEntry("one.txt", 3, false,
                    _ => ValueTask.FromResult<Stream>(new MemoryStream([1, 2, 3])));
                Assert.That(destination.Length, Is.GreaterThan(3));
                yield return new StoredZipSourceEntry("two.txt", 0, false,
                    _ => ValueTask.FromResult<Stream>(Stream.Null));
            }
        }

        [Test]
        public async Task TemporaryPlanFile_IsRemovedOnDispose()
        {
            FileStream stream = ArchiveTemporaryFile.Create();
            string path = stream.Name;
            Assert.That(File.Exists(path), Is.True);
            await stream.DisposeAsync();
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public async Task StreamingWriter_LimitsConcurrentSourcePreparation()
        {
            StoredZipArchiveWriter writer = new(Options.Create(new ResourceConcurrencyOptions { ArchiveStreams = 1 }));
            TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource cancellation = new();
            Task first = writer.WriteAsync(Stream.Null, First());
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                Task second = writer.WriteAsync(Stream.Null, Second(), cancellation.Token);
                Assert.That(secondStarted.Task.IsCompleted, Is.False);
                await cancellation.CancelAsync();
                Assert.CatchAsync<OperationCanceledException>(async () => await second);
                Assert.That(secondStarted.Task.IsCompleted, Is.False);
            }
            finally
            {
                releaseFirst.TrySetResult();
                await first;
            }

            async IAsyncEnumerable<StoredZipSourceEntry> First()
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
                yield break;
            }
            async IAsyncEnumerable<StoredZipSourceEntry> Second()
            {
                await Task.CompletedTask;
                secondStarted.SetResult();
                yield break;
            }
        }
    }
}
