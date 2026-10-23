// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Options;
using System.Buffers.Binary;

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
            bool hasSkippedFiles = false;
            StoredZipLengthCalculator layout = new();
            await foreach (StoredZipSourceEntry entry in entries.WithCancellation(cancellationToken))
            {
                ZipEntryPlan plan = layout.AddEntry(entry);
                ArchiveCrcState state = _checksums.Get(archiveIdentity, plan.LocalHeaderOffset);
                uint crc = await StoredZipEntryWriter.WriteAsync(output, entry, plan, state, cancellationToken);
                hasSkippedFiles |= !entry.IsDirectory && layout.CentralDirectoryOffset <= start;
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

            if (includeCentralDirectory && hasSkippedFiles)
            {
                await output.FlushAsync(cancellationToken);
                await CompleteCentralDirectoryAsync(centralDirectory, entries, output,
                    layout.CentralDirectoryOffset, archiveIdentity, cancellationToken);
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

        private async Task CompleteCentralDirectoryAsync(
            FileStream centralDirectory, IAsyncEnumerable<StoredZipSourceEntry> entries,
            ArchiveRangeWriteStream output, long directoryStart, string? archiveIdentity, CancellationToken cancellationToken)
        {
            StoredZipLengthCalculator layout = new();
            byte[] checksumBytes = new byte[sizeof(uint)];
            await foreach (StoredZipSourceEntry entry in entries.WithCancellation(cancellationToken))
            {
                long centralEntryOffset = layout.CentralDirectoryLength;
                ZipEntryPlan plan = layout.AddEntry(entry);
                if (layout.CentralDirectoryOffset > output.Start)
                {
                    break;
                }
                long checksumOffset = centralEntryOffset + StoredZipHeaders.CentralDirectoryChecksumOffset;
                if (!entry.IsDirectory && output.Includes(directoryStart + checksumOffset, sizeof(uint)))
                {
                    ArchiveCrcState state = _checksums.Get(archiveIdentity, plan.LocalHeaderOffset);
                    uint checksum = await StoredZipEntryWriter.GetChecksumAsync(entry, state, cancellationToken);
                    BinaryPrimitives.WriteUInt32LittleEndian(checksumBytes, checksum);
                    centralDirectory.Position = checksumOffset;
                    await centralDirectory.WriteAsync(checksumBytes, cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            _checksums.Dispose();
            _streamGate.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
