// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Cotton.Server.Services;

/// <summary>
/// Stores archive download ticket state.
/// </summary>
public sealed class ArchiveDownloadTicketStore(IMemoryCache _cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    private const string CacheKeyPrefix = "archive-download:";

    /// <summary>
    /// Executes store.
    /// </summary>
    public string Store(ArchiveDownloadTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        string token = CreateToken();
        _cache.Set(CacheKeyPrefix + token, ticket, Lifetime);
        return token;
    }

    /// <summary>
    /// Attempts to get value.
    /// </summary>
    public bool TryGet(string token, out ArchiveDownloadTicket ticket)
    {
        ticket = null!;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _cache.TryGetValue(CacheKeyPrefix + token, out ticket!);
    }

    private static string CreateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }
}

/// <summary>
/// Represents archive download ticket.
/// </summary>
public sealed record ArchiveDownloadTicket(
    string FileName,
    long SizeBytes,
    int EntryCount,
    IReadOnlyList<ArchiveDownloadEntry> Entries);

/// <summary>
/// Describes a archive download entry.
/// </summary>
public abstract record ArchiveDownloadEntry(string Path, long SizeBytes, bool IsDirectory) : IStoredZipEntry;

/// <summary>
/// Describes a archive download directory entry.
/// </summary>
public sealed record ArchiveDownloadDirectoryEntry : ArchiveDownloadEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArchiveDownloadDirectoryEntry"/> type.
    /// </summary>
    public ArchiveDownloadDirectoryEntry(string path)
        : base(path, 0, true)
    {
    }
}

/// <summary>
/// Describes a archive download file entry.
/// </summary>
public sealed record ArchiveDownloadFileEntry : ArchiveDownloadEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArchiveDownloadFileEntry"/> type.
    /// </summary>
    public ArchiveDownloadFileEntry(
        string path,
        long sizeBytes,
        IReadOnlyList<string> chunkHashes,
        Dictionary<string, long> chunkLengths)
        : base(path, sizeBytes, false)
    {
        ChunkHashes = chunkHashes;
        ChunkLengths = chunkLengths;
    }

    /// <summary>
    /// Gets the chunk hashes.
    /// </summary>
    public IReadOnlyList<string> ChunkHashes { get; }
    /// <summary>
    /// Gets chunk plaintext lengths keyed by chunk hash for deterministic archive streaming.
    /// </summary>
    public Dictionary<string, long> ChunkLengths { get; }
}
