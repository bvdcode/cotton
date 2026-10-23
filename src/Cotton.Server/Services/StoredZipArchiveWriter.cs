// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    public class StoredZipArchiveWriter : IDisposable
    {
        private readonly SemaphoreSlim _streamGate;
        private readonly ArchiveCrcCache _checksums = new();

        public StoredZipArchiveWriter(IOptions<ResourceConcurrencyOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Value.Validate();
            _streamGate = new SemaphoreSlim(options.Value.ArchiveStreams, options.Value.ArchiveStreams);
        }

        internal async Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
        {
            await _streamGate.WaitAsync(cancellationToken);
            return new AsyncGateLease(() => _streamGate.Release());
        }

        public async Task WriteAsync(
            Stream destination,
            IAsyncEnumerable<StoredZipSourceEntry> entries,
            CancellationToken cancellationToken = default)
        {
            await using IAsyncDisposable lease = await AcquireAsync(cancellationToken);
            await WriteRangeAsync(destination, entries, 0, long.MaxValue, 0, null, cancellationToken);
        }

        internal async Task WriteRangeAsync(
            Stream destination, IAsyncEnumerable<StoredZipSourceEntry> entries,
            long start, long length, long centralDirectoryOffset, string? archiveIdentity,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(entries);
            await using ArchiveRangeWriteStream output = new(destination, start, length);
            await using FileStream centralDirectory = ArchiveTemporaryFile.Create();
            bool includeCentralDirectory = output.End > centralDirectoryOffset;
            StoredZipLengthCalculator layout = new();
            await foreach (StoredZipSourceEntry entry in entries.WithCancellation(cancellationToken))
            {
                ZipEntryPlan plan = layout.AddEntry(entry);
                ArchiveCrcState state = _checksums.Get(archiveIdentity, plan.LocalHeaderOffset);
                uint crc = await StoredZipEntryWriter.WriteAsync(output, entry, plan, state, includeCentralDirectory, cancellationToken);
                if (output.Completed)
                {
                    return;
                }
                if (includeCentralDirectory)
                {
                    await StoredZipHeaders.WriteCentralDirectoryEntryAsync(
                        centralDirectory, new WrittenZipEntry(plan, crc), cancellationToken);
                }
            }

            await centralDirectory.FlushAsync(cancellationToken);
            long skip = Math.Clamp(start - output.Position, 0, centralDirectory.Length);
            centralDirectory.Position = skip;
            output.Advance(skip);
            await centralDirectory.CopyToAsync(output, cancellationToken);
            if (layout.NeedsZip64End)
            {
                await StoredZipHeaders.WriteZip64EndAsync(output, layout.EntryCount, layout, cancellationToken);
            }
            await StoredZipHeaders.WriteEndOfCentralDirectoryAsync(output, layout.EntryCount, layout, cancellationToken);
        }

        public void Dispose()
        {
            _checksums.Dispose();
            _streamGate.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
