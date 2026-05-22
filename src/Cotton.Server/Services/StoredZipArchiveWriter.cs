// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Cotton.Server.Services;

/// <summary>
/// Describes an archive entry whose final byte length is known before the response starts.
/// </summary>
public interface IStoredZipEntry
{
    /// <summary>
    /// Gets the ZIP entry path using forward slashes.
    /// </summary>
    string Path { get; }
    /// <summary>
    /// Gets the uncompressed entry size in bytes.
    /// </summary>
    long SizeBytes { get; }
    /// <summary>
    /// Indicates whether the ZIP entry represents a directory marker.
    /// </summary>
    bool IsDirectory { get; }
}

/// <summary>
/// Provides a ZIP entry path, fixed uncompressed length, and deferred stream opener.
/// </summary>
public sealed record StoredZipSourceEntry(
    string Path,
    long SizeBytes,
    bool IsDirectory,
    Func<CancellationToken, ValueTask<Stream>> OpenReadAsync) : IStoredZipEntry;

/// <summary>
/// Writes uncompressed UTF-8 ZIP archives directly to a response stream with a deterministic Content-Length.
/// </summary>
public sealed class StoredZipArchiveWriter
{
    private const ushort Utf8Flag = 1 << 11;
    private const ushort DataDescriptorFlag = 1 << 3;
    private const ushort StoreMethod = 0;
    private const ushort VersionStore = 20;
    private const ushort VersionZip64 = 45;
    private const uint UInt32Max = uint.MaxValue;
    private const ushort UInt16Max = ushort.MaxValue;

    /// <summary>
    /// Calculates the exact number of bytes that <see cref="WriteAsync"/> will emit for the entries.
    /// </summary>
    public static long CalculateLength<TEntry>(IReadOnlyList<TEntry> entries)
        where TEntry : IStoredZipEntry
    {
        return BuildPlan(entries).TotalLength;
    }

    internal static bool RequiresZip64CentralDirectoryMetadata(long sizeBytes, long localHeaderOffset)
    {
        return sizeBytes > UInt32Max || localHeaderOffset > UInt32Max;
    }

    /// <summary>
    /// Streams the entries as a STORED ZIP archive without buffering file contents in memory.
    /// </summary>
    public async Task WriteAsync(
        Stream destination,
        IReadOnlyList<StoredZipSourceEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(entries);

        ZipPlan plan = BuildPlan(entries);
        var writtenEntries = new List<WrittenZipEntry>(entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            StoredZipSourceEntry entry = entries[i];
            ZipEntryPlan entryPlan = plan.Entries[i];
            uint crc = await WriteLocalEntryAsync(destination, entry, entryPlan, cancellationToken);
            writtenEntries.Add(new WrittenZipEntry(entryPlan, crc));
        }

        await WriteCentralDirectoryAsync(destination, writtenEntries, plan, cancellationToken);
    }

    // Planning is separate from writing so the API can set Content-Length before sending bytes.
    // We deliberately use the STORED method: no compression means predictable size, lower CPU,
    // and UTF-8 filenames via the ZIP language-encoding flag instead of platform code pages.
    private static ZipPlan BuildPlan<TEntry>(IReadOnlyList<TEntry> entries)
        where TEntry : IStoredZipEntry
    {
        long offset = 0;
        var plans = new ZipEntryPlan[entries.Count];

        for (int i = 0; i < entries.Count; i++)
        {
            TEntry entry = entries[i];
            if (entry.SizeBytes < 0)
            {
                throw new InvalidOperationException($"Archive entry '{entry.Path}' has a negative size.");
            }

            byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
            if (pathBytes.Length == 0 || pathBytes.Length > UInt16Max)
            {
                throw new InvalidOperationException($"Archive entry path has invalid UTF-8 length: '{entry.Path}'.");
            }

            bool zip64DataDescriptor = !entry.IsDirectory && entry.SizeBytes > UInt32Max;
            long localHeaderLength = 30 + pathBytes.Length;
            long dataDescriptorLength = entry.IsDirectory ? 0 : zip64DataDescriptor ? 24 : 16;

            plans[i] = new ZipEntryPlan(
                entry.Path,
                pathBytes,
                entry.SizeBytes,
                entry.IsDirectory,
                zip64DataDescriptor,
                offset);

            offset += localHeaderLength + entry.SizeBytes + dataDescriptorLength;
        }

        long centralDirectoryOffset = offset;
        long centralDirectoryLength = 0;
        for (int i = 0; i < plans.Length; i++)
        {
            ZipEntryPlan entry = plans[i];
            long centralExtraLength = GetCentralZip64ExtraLength(entry.SizeBytes, entry.LocalHeaderOffset);
            plans[i] = entry with { CentralExtraLength = centralExtraLength };
            centralDirectoryLength += 46 + entry.PathBytes.Length + centralExtraLength;
        }

        bool needsZip64End = entries.Count > UInt16Max ||
            centralDirectoryOffset > UInt32Max ||
            centralDirectoryLength > UInt32Max;
        long endLength = (needsZip64End ? 56 + 20 : 0) + 22;
        return new ZipPlan(
            plans,
            centralDirectoryOffset,
            centralDirectoryLength,
            needsZip64End,
            centralDirectoryOffset + centralDirectoryLength + endLength);
    }

    private static async Task<uint> WriteLocalEntryAsync(
        Stream destination,
        StoredZipSourceEntry entry,
        ZipEntryPlan plan,
        CancellationToken cancellationToken)
    {
        ushort flags = Utf8Flag;
        if (!entry.IsDirectory)
        {
            flags |= DataDescriptorFlag;
        }

        ushort versionNeeded = plan.UsesZip64DataDescriptor ? VersionZip64 : VersionStore;
        await WriteLocalHeaderAsync(destination, plan, flags, versionNeeded, cancellationToken);

        if (entry.IsDirectory)
        {
            return 0;
        }

        var crc = new Crc32Accumulator();
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

        await WriteDataDescriptorAsync(destination, crc.Value, entry.SizeBytes, plan.UsesZip64DataDescriptor, cancellationToken)
            .ConfigureAwait(false);
        return crc.Value;
    }

    private static async Task WriteLocalHeaderAsync(
        Stream destination,
        ZipEntryPlan entry,
        ushort flags,
        ushort versionNeeded,
        CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(30);
        try
        {
            Span<byte> header = buffer.AsSpan(0, 30);
            BinaryPrimitives.WriteUInt32LittleEndian(header[0..4], 0x04034b50);
            BinaryPrimitives.WriteUInt16LittleEndian(header[4..6], versionNeeded);
            BinaryPrimitives.WriteUInt16LittleEndian(header[6..8], flags);
            BinaryPrimitives.WriteUInt16LittleEndian(header[8..10], StoreMethod);
            BinaryPrimitives.WriteUInt16LittleEndian(header[10..12], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(header[12..14], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(header[14..18], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(header[18..22], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(header[22..26], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(header[26..28], checked((ushort)entry.PathBytes.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(header[28..30], 0);
            await destination.WriteAsync(header.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        await destination.WriteAsync(entry.PathBytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteDataDescriptorAsync(
        Stream destination,
        uint crc,
        long sizeBytes,
        bool useZip64,
        CancellationToken cancellationToken)
    {
        int length = useZip64 ? 24 : 16;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            Span<byte> descriptor = buffer.AsSpan(0, length);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor[0..4], 0x08074b50);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor[4..8], crc);
            if (useZip64)
            {
                BinaryPrimitives.WriteInt64LittleEndian(descriptor[8..16], sizeBytes);
                BinaryPrimitives.WriteInt64LittleEndian(descriptor[16..24], sizeBytes);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(descriptor[8..12], checked((uint)sizeBytes));
                BinaryPrimitives.WriteUInt32LittleEndian(descriptor[12..16], checked((uint)sizeBytes));
            }

            await destination.WriteAsync(descriptor.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task WriteCentralDirectoryAsync(
        Stream destination,
        IReadOnlyList<WrittenZipEntry> entries,
        ZipPlan plan,
        CancellationToken cancellationToken)
    {
        foreach (WrittenZipEntry written in entries)
        {
            await WriteCentralDirectoryEntryAsync(destination, written, cancellationToken).ConfigureAwait(false);
        }

        if (plan.NeedsZip64End)
        {
            await WriteZip64EndAsync(destination, entries.Count, plan, cancellationToken).ConfigureAwait(false);
        }

        await WriteEndOfCentralDirectoryAsync(destination, entries.Count, plan, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteCentralDirectoryEntryAsync(
        Stream destination,
        WrittenZipEntry written,
        CancellationToken cancellationToken)
    {
        ZipEntryPlan entry = written.Plan;
        bool sizeNeedsZip64 = entry.SizeBytes > UInt32Max;
        bool offsetNeedsZip64 = entry.LocalHeaderOffset > UInt32Max;
        // ZIP64 central metadata is required even when each file is small if the local
        // header offset crosses the 4 GiB boundary in a large folder archive.
        bool requiresZip64CentralMetadata = RequiresZip64CentralDirectoryMetadata(
            entry.SizeBytes,
            entry.LocalHeaderOffset);
        ushort versionNeeded = requiresZip64CentralMetadata ? VersionZip64 : VersionStore;
        ushort versionMadeBy = (ushort)((3 << 8) | versionNeeded);
        ushort flags = Utf8Flag;
        if (!entry.IsDirectory)
        {
            flags |= DataDescriptorFlag;
        }

        const int fixedLength = 46;
        int extraLength = checked((int)entry.CentralExtraLength);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(fixedLength);
        try
        {
            Span<byte> header = buffer.AsSpan(0, fixedLength);
            BinaryPrimitives.WriteUInt32LittleEndian(header[0..4], 0x02014b50);
            BinaryPrimitives.WriteUInt16LittleEndian(header[4..6], versionMadeBy);
            BinaryPrimitives.WriteUInt16LittleEndian(header[6..8], versionNeeded);
            BinaryPrimitives.WriteUInt16LittleEndian(header[8..10], flags);
            BinaryPrimitives.WriteUInt16LittleEndian(header[10..12], StoreMethod);
            BinaryPrimitives.WriteUInt16LittleEndian(header[12..14], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(header[14..16], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(header[16..20], written.Crc32);
            BinaryPrimitives.WriteUInt32LittleEndian(header[20..24], sizeNeedsZip64 ? UInt32Max : checked((uint)entry.SizeBytes));
            BinaryPrimitives.WriteUInt32LittleEndian(header[24..28], sizeNeedsZip64 ? UInt32Max : checked((uint)entry.SizeBytes));
            BinaryPrimitives.WriteUInt16LittleEndian(header[28..30], checked((ushort)entry.PathBytes.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(header[30..32], checked((ushort)extraLength));
            BinaryPrimitives.WriteUInt16LittleEndian(header[32..34], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(header[34..36], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(header[36..38], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(header[38..42], entry.IsDirectory ? 0x10u : 0u);
            BinaryPrimitives.WriteUInt32LittleEndian(header[42..46], offsetNeedsZip64 ? UInt32Max : checked((uint)entry.LocalHeaderOffset));

            await destination.WriteAsync(header.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        await destination.WriteAsync(entry.PathBytes, cancellationToken).ConfigureAwait(false);
        if (extraLength > 0)
        {
            await WriteZip64CentralExtraAsync(
                destination,
                entry,
                sizeNeedsZip64,
                offsetNeedsZip64,
                extraLength,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteZip64CentralExtraAsync(
        Stream destination,
        ZipEntryPlan entry,
        bool sizeNeedsZip64,
        bool offsetNeedsZip64,
        int extraLength,
        CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(extraLength);
        try
        {
            Span<byte> extra = buffer.AsSpan(0, extraLength);
            WriteZip64CentralExtra(extra, entry, sizeNeedsZip64, offsetNeedsZip64);
            await destination.WriteAsync(extra.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void WriteZip64CentralExtra(
        Span<byte> destination,
        ZipEntryPlan entry,
        bool sizeNeedsZip64,
        bool offsetNeedsZip64)
    {
        int payloadLength = (sizeNeedsZip64 ? 16 : 0) + (offsetNeedsZip64 ? 8 : 0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[0..2], 0x0001);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..4], checked((ushort)payloadLength));
        int cursor = 4;
        if (sizeNeedsZip64)
        {
            BinaryPrimitives.WriteInt64LittleEndian(destination[cursor..(cursor + 8)], entry.SizeBytes);
            cursor += 8;
            BinaryPrimitives.WriteInt64LittleEndian(destination[cursor..(cursor + 8)], entry.SizeBytes);
            cursor += 8;
        }

        if (offsetNeedsZip64)
        {
            BinaryPrimitives.WriteInt64LittleEndian(destination[cursor..(cursor + 8)], entry.LocalHeaderOffset);
        }
    }

    private static async Task WriteZip64EndAsync(
        Stream destination,
        int entryCount,
        ZipPlan plan,
        CancellationToken cancellationToken)
    {
        long zip64EndOffset = plan.CentralDirectoryOffset + plan.CentralDirectoryLength;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(76);
        try
        {
            Span<byte> zip64 = buffer.AsSpan(0, 56);
            BinaryPrimitives.WriteUInt32LittleEndian(zip64[0..4], 0x06064b50);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[4..12], 44);
            BinaryPrimitives.WriteUInt16LittleEndian(zip64[12..14], VersionZip64);
            BinaryPrimitives.WriteUInt16LittleEndian(zip64[14..16], VersionZip64);
            BinaryPrimitives.WriteUInt32LittleEndian(zip64[16..20], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(zip64[20..24], 0);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[24..32], entryCount);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[32..40], entryCount);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[40..48], plan.CentralDirectoryLength);
            BinaryPrimitives.WriteInt64LittleEndian(zip64[48..56], plan.CentralDirectoryOffset);

            Span<byte> locator = buffer.AsSpan(56, 20);
            BinaryPrimitives.WriteUInt32LittleEndian(locator[0..4], 0x07064b50);
            BinaryPrimitives.WriteUInt32LittleEndian(locator[4..8], 0);
            BinaryPrimitives.WriteInt64LittleEndian(locator[8..16], zip64EndOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(locator[16..20], 1);

            await destination.WriteAsync(buffer.AsMemory(0, 76), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task WriteEndOfCentralDirectoryAsync(
        Stream destination,
        int entryCount,
        ZipPlan plan,
        CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(22);
        try
        {
            Span<byte> eocd = buffer.AsSpan(0, 22);
            BinaryPrimitives.WriteUInt32LittleEndian(eocd[0..4], 0x06054b50);
            BinaryPrimitives.WriteUInt16LittleEndian(eocd[4..6], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(eocd[6..8], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(eocd[8..10], entryCount > UInt16Max ? UInt16Max : (ushort)entryCount);
            BinaryPrimitives.WriteUInt16LittleEndian(eocd[10..12], entryCount > UInt16Max ? UInt16Max : (ushort)entryCount);
            BinaryPrimitives.WriteUInt32LittleEndian(eocd[12..16], plan.CentralDirectoryLength > UInt32Max ? UInt32Max : (uint)plan.CentralDirectoryLength);
            BinaryPrimitives.WriteUInt32LittleEndian(eocd[16..20], plan.CentralDirectoryOffset > UInt32Max ? UInt32Max : (uint)plan.CentralDirectoryOffset);
            BinaryPrimitives.WriteUInt16LittleEndian(eocd[20..22], 0);
            await destination.WriteAsync(eocd.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static long GetCentralZip64ExtraLength(long sizeBytes, long localHeaderOffset)
    {
        int payloadLength = 0;
        if (sizeBytes > UInt32Max)
        {
            payloadLength += 16;
        }

        if (localHeaderOffset > UInt32Max)
        {
            payloadLength += 8;
        }

        return payloadLength == 0 ? 0 : 4 + payloadLength;
    }

    private sealed record ZipPlan(
        ZipEntryPlan[] Entries,
        long CentralDirectoryOffset,
        long CentralDirectoryLength,
        bool NeedsZip64End,
        long TotalLength);

    private sealed record ZipEntryPlan(
        string Path,
        byte[] PathBytes,
        long SizeBytes,
        bool IsDirectory,
        bool UsesZip64DataDescriptor,
        long LocalHeaderOffset)
    {
        /// <summary>
        /// Gets or sets the central extra length.
        /// </summary>
        public long CentralExtraLength { get; init; }
    }

    private sealed record WrittenZipEntry(ZipEntryPlan Plan, uint Crc32);
}
