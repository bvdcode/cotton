// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Cotton.Server.Services;

public sealed class ArchiveDownloadTicketStore(IMemoryCache _cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    private const string CacheKeyPrefix = "archive-download:";

    public string Store(ArchiveDownloadTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        string token = CreateToken();
        _cache.Set(CacheKeyPrefix + token, ticket, Lifetime);
        return token;
    }

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

public sealed record ArchiveDownloadTicket(
    string FileName,
    long SizeBytes,
    int EntryCount,
    IReadOnlyList<ArchiveDownloadEntry> Entries);

public abstract record ArchiveDownloadEntry(string Path, long SizeBytes, bool IsDirectory) : IStoredZipEntry;

public sealed record ArchiveDownloadDirectoryEntry : ArchiveDownloadEntry
{
    public ArchiveDownloadDirectoryEntry(string path)
        : base(path, 0, true)
    {
    }
}

public sealed record ArchiveDownloadFileEntry : ArchiveDownloadEntry
{
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

    public IReadOnlyList<string> ChunkHashes { get; }
    public Dictionary<string, long> ChunkLengths { get; }
}
