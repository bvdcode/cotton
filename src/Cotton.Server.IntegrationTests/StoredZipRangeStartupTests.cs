// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Options;

namespace Cotton.Server.IntegrationTests
{
    public class StoredZipRangeStartupTests
    {
        [TestCase(1)]
        [TestCase(22)]
        [TestCase(98)]
        public async Task ColdZip64EndRange_DoesNotReadFileContents(int length)
        {
            using StoredZipArchiveWriter writer = new(Options.Create(new ResourceConcurrencyOptions()));
            StoredZipSourceEntry entry = new("huge.bin", 117L * 1024 * 1024 * 1024, false,
                _ => throw new AssertionException("ZIP end records do not require file checksums."));
            StoredZipLengthCalculator layout = new();
            layout.Add(entry);
            using MemoryStream result = new();
            await writer.WriteRangeAsync(result, new[] { entry }.ToAsyncEnumerable(), layout.TotalLength - length,
                length, layout.CentralDirectoryOffset, "end-range", CancellationToken.None);
            Assert.That(result.Length, Is.EqualTo(length));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ColdOpenEndedRange_WritesRequestedContentBeforeReadingSkippedContent(bool separateFiles)
        {
            const long skipped = 60L * 1024 * 1024 * 1024;
            const long size = 117L * 1024 * 1024 * 1024;
            long bytesRead = 0;
            using StoredZipArchiveWriter writer = new(Options.Create(new ResourceConcurrencyOptions()));
            StoredZipSourceEntry payload = new("payload.bin", size, false,
                _ => ValueTask.FromResult<Stream>(new ArchiveProbeStream(size, (position, count) =>
                {
                    if (!separateFiles)
                    {
                        Assert.That(position, Is.GreaterThanOrEqualTo(skipped), "Skipped bytes delayed the first response body.");
                    }
                    bytesRead += count;
                })));
            StoredZipSourceEntry prefix = new("prefix.bin", skipped, false,
                _ => throw new AssertionException("An earlier file was opened before the requested response body."));
            StoredZipSourceEntry[] entries = separateFiles ? [prefix, payload] : [payload];
            StoredZipLengthCalculator layout = new();
            long start = 0;
            foreach (StoredZipSourceEntry entry in entries)
            {
                ZipEntryPlan plan = layout.AddEntry(entry);
                start = plan.LocalHeaderOffset + 30 + plan.PathBytes.Length + (separateFiles ? 0 : skipped);
            }
            using InterruptedArchiveStream destination = new(0);
            await Assert.ThrowsAsync<IOException>(() => writer.WriteRangeAsync(destination, entries.ToAsyncEnumerable(),
                start, layout.TotalLength - start, layout.CentralDirectoryOffset, "cold", CancellationToken.None));
            Assert.That(bytesRead, Is.EqualTo(128 * 1024));
        }

        [TestCase(0)]
        [TestCase(100_000)]
        [TestCase(262_144)]
        [TestCase(400_000)]
        [TestCase(1_048_576)]
        public async Task PartialEntry_UsesConcurrentChecksumProgressWithoutRereadingCompletedPrefix(int progress)
        {
            const int size = 1024 * 1024;
            const int offset = size / 4;
            Crc32Accumulator concurrent = new();
            Crc32Accumulator expected = new();
            byte[] bytes = Enumerable.Range(0, size).Select(position => (byte)(position % 251)).ToArray();
            concurrent.Append(bytes.AsSpan(0, progress));
            expected.Append(bytes);
            ArchiveCrcState state = new();
            bool advanced = false;
            long readBytes = 0;
            StoredZipSourceEntry entry = new("file.bin", size, false,
                _ => ValueTask.FromResult<Stream>(new ArchiveProbeStream(size, (position, count) =>
                {
                    readBytes += count;
                    if (!advanced && position + count == size)
                    {
                        state.Advance(progress, concurrent.Value);
                        advanced = true;
                    }
                })));
            StoredZipLengthCalculator layout = new();
            ZipEntryPlan plan = layout.AddEntry(entry);
            long start = 30 + plan.PathBytes.Length + offset;
            using MemoryStream body = new();
            await using ArchiveRangeWriteStream range = new(body, start, layout.TotalLength - start);
            uint checksum = await StoredZipEntryWriter.WriteAsync(range, entry, plan, state, CancellationToken.None);
            long missing = progress <= offset ? offset - progress : size - progress;
            Assert.Multiple(() =>
            {
                Assert.That(checksum, Is.EqualTo(expected.Value));
                Assert.That(state.Read().Checksum, Is.EqualTo(expected.Value));
                Assert.That(state.Read().Position, Is.EqualTo(size));
                Assert.That(readBytes, Is.EqualTo(size - offset + missing));
                Assert.That(body.ToArray().AsSpan(0, size - offset).ToArray(), Is.EqualTo(bytes.AsSpan(offset).ToArray()));
            });
        }
    }
}
