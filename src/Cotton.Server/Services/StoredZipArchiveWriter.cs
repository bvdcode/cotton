// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers;
using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    public class StoredZipArchiveWriter
    {
        private readonly SemaphoreSlim _streamGate;

        public StoredZipArchiveWriter(IOptions<ResourceConcurrencyOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Value.Validate();
            _streamGate = new SemaphoreSlim(options.Value.ArchiveStreams, options.Value.ArchiveStreams);
        }

        public async Task WriteAsync(
            Stream destination,
            IAsyncEnumerable<StoredZipSourceEntry> entries,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(entries);
            await _streamGate.WaitAsync(cancellationToken);
            try
            {
                await using FileStream centralDirectory = ArchiveTemporaryFile.Create();
                StoredZipLengthCalculator layout = new();
                await foreach (StoredZipSourceEntry entry in entries.WithCancellation(cancellationToken))
                {
                    ZipEntryPlan plan = layout.AddEntry(entry);
                    uint crc = await WriteLocalEntryAsync(destination, entry, plan, cancellationToken);
                    await StoredZipHeaders.WriteCentralDirectoryEntryAsync(
                        centralDirectory, new WrittenZipEntry(plan, crc), cancellationToken);
                }

                await centralDirectory.FlushAsync(cancellationToken);
                centralDirectory.Position = 0;
                await centralDirectory.CopyToAsync(destination, cancellationToken);
                if (layout.NeedsZip64End)
                {
                    await StoredZipHeaders.WriteZip64EndAsync(destination, layout.EntryCount, layout, cancellationToken);
                }
                await StoredZipHeaders.WriteEndOfCentralDirectoryAsync(destination, layout.EntryCount, layout, cancellationToken);
            }
            finally
            {
                _streamGate.Release();
            }
        }

        private static async Task<uint> WriteLocalEntryAsync(
            Stream destination,
            StoredZipSourceEntry entry,
            ZipEntryPlan plan,
            CancellationToken cancellationToken)
        {
            ushort flags = StoredZipHeaders.Utf8Flag;
            if (!entry.IsDirectory)
            {
                flags |= StoredZipHeaders.DataDescriptorFlag;
            }

            ushort versionNeeded = plan.UsesZip64DataDescriptor ? StoredZipHeaders.VersionZip64 : StoredZipHeaders.VersionStore;
            await StoredZipHeaders.WriteLocalHeaderAsync(destination, plan, flags, versionNeeded, cancellationToken);

            if (entry.IsDirectory)
            {
                return 0;
            }

            Crc32Accumulator crc = new();
            long bytesWritten = 0;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
            try
            {
                await using Stream source = await entry.OpenReadAsync(cancellationToken).ConfigureAwait(false);
                while (true)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    crc.Append(buffer.AsSpan(0, read));
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    bytesWritten += read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (bytesWritten != entry.SizeBytes)
            {
                throw new InvalidOperationException(
                    $"Archive entry '{entry.Path}' expected {entry.SizeBytes} bytes but streamed {bytesWritten} bytes.");
            }

            await StoredZipHeaders.WriteDataDescriptorAsync(destination, crc.Value, entry.SizeBytes, plan.UsesZip64DataDescriptor, cancellationToken)
                .ConfigureAwait(false);
            return crc.Value;
        }
    }
}
