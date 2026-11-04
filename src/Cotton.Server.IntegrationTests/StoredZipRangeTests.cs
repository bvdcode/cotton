// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Options;

namespace Cotton.Server.IntegrationTests
{
    public class StoredZipRangeTests
    {
        [Test]
        public void ChecksumCheckpoint_ContinuesStandardZipCrc32()
        {
            Crc32Accumulator first = new();
            first.Append("1234"u8);
            Crc32Accumulator continued = new(first.Value);
            continued.Append("56789"u8);
            Assert.That(continued.Value, Is.EqualTo(0xcbf43926u));
        }

        [TestCase(1, 2)]
        [TestCase(2, 1)]
        [TestCase(0, 1)]
        public async Task FullDownload_RejectsMismatchedSourceLength(int declared, int actual)
        {
            using StoredZipArchiveWriter writer = CreateWriter();
            StoredZipSourceEntry entry = new("invalid", declared, false,
                _ => ValueTask.FromResult<Stream>(new MemoryStream(new byte[actual])));
            await Assert.CatchAsync<IOException>(() => writer.WriteAsync(Stream.Null, new[] { entry }.ToAsyncEnumerable()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RangesAcrossEveryZipStructure_MatchFullArchive(bool warmCache)
        {
            using StoredZipArchiveWriter writer = CreateWriter();
            StoredZipSourceEntry[] entries =
            [
                new("folder/", 0, true, _ => ValueTask.FromResult<Stream>(Stream.Null)),
                Entry("folder/данные.bin", 19),
                Entry("empty", 0),
                Entry("other.bin", 47),
            ];
            StoredZipLengthCalculator layout = Layout(entries);
            using MemoryStream full = new();
            await writer.WriteRangeAsync(full, entries.ToAsyncEnumerable(), 0, layout.TotalLength,
                layout.CentralDirectoryOffset, warmCache ? "archive" : null, CancellationToken.None);
            byte[] expected = full.ToArray();
            for (int start = 0; start < expected.Length; start++)
            {
                using MemoryStream part = new();
                int length = Math.Min(37, expected.Length - start);
                await writer.WriteRangeAsync(part, entries.ToAsyncEnumerable(), start, length,
                    layout.CentralDirectoryOffset, warmCache ? "archive" : null, CancellationToken.None);
                Assert.That(part.ToArray(), Is.EqualTo(expected.AsSpan(start, length).ToArray()), $"Offset {start}");
            }
        }

        [Test]
        public async Task InterruptedDownload_ResumesInsideFile_WithoutRereadingItsPrefix()
        {
            using StoredZipArchiveWriter writer = CreateWriter();
            long bytesRead = 0;
            StoredZipSourceEntry[] entries = [Entry("large.bin", 8 * 1024 * 1024, (_, count) => bytesRead += count), Entry("tail", 15)];
            StoredZipLengthCalculator layout = Layout(entries);
            using MemoryStream full = new();
            await writer.WriteAsync(full, entries.ToAsyncEnumerable());
            using InterruptedArchiveStream first = new(4 * 1024 * 1024);
            await Assert.ThrowsAsync<IOException>(() => writer.WriteRangeAsync(first, entries.ToAsyncEnumerable(),
                0, layout.TotalLength, layout.CentralDirectoryOffset, "resume", CancellationToken.None));
            bytesRead = 0;
            using MemoryStream remainder = new();
            await writer.WriteRangeAsync(remainder, entries.ToAsyncEnumerable(), first.Length,
                layout.TotalLength - first.Length, layout.CentralDirectoryOffset, "resume", CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(first.ToArray().Concat(remainder.ToArray()).ToArray(), Is.EqualTo(full.ToArray()));
                Assert.That(bytesRead, Is.LessThanOrEqualTo(layout.TotalLength - first.Length));
            });
        }

        [Test]
        public async Task WarmArchive_ResumesWithoutOpeningCompletedFiles()
        {
            using StoredZipArchiveWriter writer = CreateWriter();
            bool forbidPrefix = false;
            StoredZipSourceEntry[] entries = [Entry("first", 4096, (_, _) =>
            {
                Assert.That(forbidPrefix, Is.False, "Completed source was read again.");
            }), Entry("second", 256)];
            StoredZipLengthCalculator layout = Layout(entries);
            using MemoryStream full = new();
            await writer.WriteRangeAsync(full, entries.ToAsyncEnumerable(), 0, layout.TotalLength,
                layout.CentralDirectoryOffset, "same-content-new-link", CancellationToken.None);
            forbidPrefix = true;
            int start = 30 + "first".Length + 4096 + 16 + 30 + "second".Length + 100;
            using MemoryStream tail = new();
            await writer.WriteRangeAsync(tail, entries.ToAsyncEnumerable(), start, layout.TotalLength - start,
                layout.CentralDirectoryOffset, "same-content-new-link", CancellationToken.None);
            Assert.That(tail.ToArray(), Is.EqualTo(full.ToArray().AsSpan(start).ToArray()));
        }

        [Test]
        public async Task RangeInside117GiBFile_SeeksAndReadsOnlyRequestedKilobyte()
        {
            const long size = 117L * 1024 * 1024 * 1024;
            const long offset = 60L * 1024 * 1024 * 1024;
            long bytesRead = 0;
            using StoredZipArchiveWriter writer = CreateWriter();
            StoredZipSourceEntry[] entries = [Entry("huge.bin", size, (position, count) =>
            {
                Assert.That(position, Is.GreaterThanOrEqualTo(offset));
                bytesRead += count;
            })];
            StoredZipLengthCalculator layout = Layout(entries);
            using MemoryStream result = new();
            await writer.WriteRangeAsync(result, entries.ToAsyncEnumerable(), 30 + "huge.bin".Length + offset,
                1024, layout.CentralDirectoryOffset, "huge", CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(bytesRead, Is.EqualTo(1024));
                Assert.That(result.Length, Is.EqualTo(1024));
                Assert.That(result.ToArray(), Is.EqualTo(Enumerable.Range(0, 1024).Select(i => (byte)((offset + i) % 251)).ToArray()));
            });
        }

        [Test]
        public async Task ConcurrentRanges_MatchFullArchive()
        {
            using StoredZipArchiveWriter writer = CreateWriter();
            StoredZipSourceEntry[] entries = [Entry("shared.bin", 1024 * 1024)];
            StoredZipLengthCalculator layout = Layout(entries);
            using MemoryStream full = new();
            await writer.WriteAsync(full, entries.ToAsyncEnumerable());
            byte[] expected = full.ToArray();
            await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
            {
                int start = index * 100_000;
                using MemoryStream part = new();
                await writer.WriteRangeAsync(part, entries.ToAsyncEnumerable(), start, expected.Length - start,
                    layout.CentralDirectoryOffset, "concurrent", CancellationToken.None);
                Assert.That(part.ToArray(), Is.EqualTo(expected.AsSpan(start).ToArray()));
            }));
        }

        private static StoredZipSourceEntry Entry(string name, long size, Action<long, int>? read = null)
            => new(name, size, false, _ => ValueTask.FromResult<Stream>(new ArchiveProbeStream(size, read ?? ((_, _) => { }))));

        private static StoredZipArchiveWriter CreateWriter() => new(Options.Create(new ResourceConcurrencyOptions()));

        private static StoredZipLengthCalculator Layout(IEnumerable<StoredZipSourceEntry> entries)
        {
            StoredZipLengthCalculator result = new();
            foreach (StoredZipSourceEntry entry in entries)
            {
                result.Add(entry);
            }
            return result;
        }
    }
}
