// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers;

namespace Cotton.Server.Services
{
    internal static class StoredZipEntryWriter
    {
        public static async Task<uint> WriteAsync(
            ArchiveRangeWriteStream destination, StoredZipSourceEntry entry, ZipEntryPlan plan,
            ArchiveCrcState state, bool includeCentralDirectory, CancellationToken cancellationToken)
        {
            ushort flags = StoredZipHeaders.Utf8Flag;
            if (!entry.IsDirectory)
            {
                flags |= StoredZipHeaders.DataDescriptorFlag;
            }
            ushort version = plan.UsesZip64DataDescriptor ? StoredZipHeaders.VersionZip64 : StoredZipHeaders.VersionStore;
            await StoredZipHeaders.WriteLocalHeaderAsync(destination, plan, flags, version, cancellationToken);
            if (entry.IsDirectory || destination.Completed)
            {
                return 0;
            }

            int descriptorLength = plan.UsesZip64DataDescriptor ? 24 : 16;
            bool needsChecksum = includeCentralDirectory || destination.Includes(destination.Position + entry.SizeBytes, descriptorLength);
            uint checksum = await WriteContentAsync(destination, entry, state, needsChecksum, cancellationToken);
            if (!destination.Completed)
            {
                await StoredZipHeaders.WriteDataDescriptorAsync(destination, checksum, entry.SizeBytes,
                    plan.UsesZip64DataDescriptor, cancellationToken);
            }
            return checksum;
        }

        private static async Task<uint> WriteContentAsync(
            ArchiveRangeWriteStream destination, StoredZipSourceEntry entry, ArchiveCrcState state,
            bool needsChecksum, CancellationToken cancellationToken)
        {
            long contentStart = destination.Position;
            long first = Math.Clamp(destination.Start - contentStart, 0, entry.SizeBytes);
            long last = Math.Clamp(destination.End - contentStart, 0, entry.SizeBytes);
            ArchiveCrcCheckpoint checkpoint = state.Read();
            if (needsChecksum && checkpoint.Position < entry.SizeBytes)
            {
                first = Math.Min(first, checkpoint.Position);
                last = entry.SizeBytes;
            }
            Crc32Accumulator crc = new(checkpoint.Checksum);
            if (first < last || (entry.SizeBytes == 0 && needsChecksum))
            {
                await CopyContentAsync(destination, entry, state, checkpoint, crc, first, last, cancellationToken);
            }
            destination.Advance(contentStart + entry.SizeBytes - destination.Position);
            return crc.Value;
        }

        private static async Task CopyContentAsync(
            ArchiveRangeWriteStream destination, StoredZipSourceEntry entry, ArchiveCrcState state,
            ArchiveCrcCheckpoint checkpoint, Crc32Accumulator crc, long first, long last, CancellationToken cancellationToken)
        {
            await using Stream source = await entry.OpenReadAsync(cancellationToken);
            if (first > 0)
            {
                source.Seek(first, SeekOrigin.Begin);
            }
            destination.Advance(first);
            bool trackChecksum = first <= checkpoint.Position;
            long position = first;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
            try
            {
                while (position < last)
                {
                    int count = (int)Math.Min(buffer.Length, last - position);
                    int read = await source.ReadAsync(buffer.AsMemory(0, count), cancellationToken);
                    if (read == 0)
                    {
                        throw new EndOfStreamException($"Archive entry '{entry.Path}' ended before its declared size.");
                    }
                    if (trackChecksum && position + read > checkpoint.Position)
                    {
                        int skip = (int)Math.Max(0, checkpoint.Position - position);
                        crc.Append(buffer.AsSpan(skip, read - skip));
                        if (position + read < entry.SizeBytes)
                        {
                            state.Advance(position + read, crc.Value);
                        }
                    }
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    position += read;
                }
                if (last == entry.SizeBytes && await source.ReadAsync(buffer.AsMemory(0, 1), cancellationToken) != 0)
                {
                    throw new IOException($"Archive entry '{entry.Path}' exceeds its declared size.");
                }
                if (trackChecksum)
                {
                    state.Advance(position, crc.Value);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
