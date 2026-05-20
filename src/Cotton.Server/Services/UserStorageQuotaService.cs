// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services;

// Logical quota is enforced when file references are created or changed.
// Raw chunk uploads are intentionally handled by storage-pressure protection,
// because chunking is an internal storage detail rather than user-owned data.
public class UserStorageQuotaService(
    CottonDbContext _dbContext,
    SettingsProvider _settings,
    IMemoryCache _cache)
{
    private static readonly TimeSpan UsedBytesCacheDuration = TimeSpan.FromMinutes(15);
    private readonly Dictionary<Guid, long> _usedBytesByUser = [];

    public async Task<long> GetUsedBytesAsync(Guid userId, CancellationToken ct = default)
    {
        if (_usedBytesByUser.TryGetValue(userId, out long cachedUsedBytes))
        {
            return cachedUsedBytes;
        }

        string cacheKey = GetUsedBytesCacheKey(userId);
        if (_cache.TryGetValue(cacheKey, out long processCachedUsedBytes))
        {
            _usedBytesByUser[userId] = processCachedUsedBytes;
            return processCachedUsedBytes;
        }

        long? usedBytes = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .SumAsync(x => (long?)x.FileManifest.SizeBytes, ct);

        long resolvedUsedBytes = usedBytes ?? 0;
        _usedBytesByUser[userId] = resolvedUsedBytes;
        _cache.Set(cacheKey, resolvedUsedBytes, UsedBytesCacheDuration);
        return resolvedUsedBytes;
    }

    public async Task<UserStorageQuotaDto> GetSnapshotAsync(Guid userId, CancellationToken ct = default)
    {
        long usedBytes = await GetUsedBytesAsync(userId, ct);
        long? quotaBytes = _settings.GetServerSettings().DefaultUserStorageQuotaBytes;
        if (quotaBytes is null or <= 0)
        {
            return new UserStorageQuotaDto
            {
                UsedBytes = usedBytes,
                QuotaBytes = null,
                AvailableBytes = null,
            };
        }

        return new UserStorageQuotaDto
        {
            UsedBytes = usedBytes,
            QuotaBytes = quotaBytes.Value,
            AvailableBytes = Math.Max(0, quotaBytes.Value - usedBytes),
        };
    }

    public async Task<long> EnsureCanAddFileReferenceAsync(
        Guid userId,
        Guid fileManifestId,
        CancellationToken ct = default)
    {
        long additionalBytes = await _dbContext.FileManifests
            .AsNoTracking()
            .Where(x => x.Id == fileManifestId)
            .Select(x => x.SizeBytes)
            .SingleAsync(ct);

        await EnsureCanAddLogicalBytesAsync(userId, additionalBytes, reserveInRequestCache: true, ct);
        return Math.Max(0, additionalBytes);
    }

    public Task EnsureCanAddKnownFileSizeAsync(
        Guid userId,
        long sizeBytes,
        CancellationToken ct = default)
    {
        return EnsureCanAddLogicalBytesAsync(userId, sizeBytes, reserveInRequestCache: false, ct);
    }

    public async Task<long> EnsureCanChangeFileManifestAsync(
        Guid userId,
        Guid nodeFileId,
        Guid newFileManifestId,
        CancellationToken ct = default)
    {
        var current = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
            .Select(x => new
            {
                x.FileManifestId,
                x.FileManifest.SizeBytes,
                x.FileManifest.ProposedContentHash
            })
            .SingleOrDefaultAsync(ct);
        if (current is null)
        {
            return 0;
        }

        var next = await _dbContext.FileManifests
            .AsNoTracking()
            .Where(x => x.Id == newFileManifestId)
            .Select(x => new
            {
                x.SizeBytes,
                x.ProposedContentHash
            })
            .SingleAsync(ct);

        long additionalBytes = current.FileManifestId == newFileManifestId
            || current.ProposedContentHash.SequenceEqual(next.ProposedContentHash)
                ? 0
                : next.SizeBytes;

        await EnsureCanAddLogicalBytesAsync(userId, additionalBytes, reserveInRequestCache: true, ct);
        return Math.Max(0, additionalBytes);
    }

    private async Task EnsureCanAddLogicalBytesAsync(
        Guid userId,
        long additionalBytes,
        bool reserveInRequestCache,
        CancellationToken ct)
    {
        long? quotaBytes = _settings.GetServerSettings().DefaultUserStorageQuotaBytes;
        if (quotaBytes is null or <= 0)
        {
            return;
        }

        long safeAdditionalBytes = Math.Max(0, additionalBytes);
        if (safeAdditionalBytes == 0)
        {
            return;
        }

        long usedBytes = await GetUsedBytesAsync(userId, ct);
        if (usedBytes > quotaBytes.Value - safeAdditionalBytes)
        {
            throw new BadRequestException<User>(
                $"Storage quota exceeded. Current usage is {usedBytes} bytes, quota is {quotaBytes.Value} bytes.");
        }

        if (reserveInRequestCache)
        {
            _usedBytesByUser[userId] = usedBytes + safeAdditionalBytes;
        }
    }
    public void RecordLogicalBytesAdded(Guid userId, long bytes)
    {
        long safeBytes = Math.Max(0, bytes);
        if (safeBytes == 0)
        {
            return;
        }

        string cacheKey = GetUsedBytesCacheKey(userId);
        if (_usedBytesByUser.TryGetValue(userId, out long scopedUsedBytes))
        {
            _cache.Set(cacheKey, scopedUsedBytes, UsedBytesCacheDuration);
            return;
        }

        if (_cache.TryGetValue(cacheKey, out long cachedUsedBytes))
        {
            _cache.Set(cacheKey, cachedUsedBytes + safeBytes, UsedBytesCacheDuration);
        }
    }

    public void RecordLogicalBytesRemoved(Guid userId, long bytes)
    {
        long safeBytes = Math.Max(0, bytes);
        if (safeBytes == 0)
        {
            return;
        }

        string cacheKey = GetUsedBytesCacheKey(userId);
        if (_usedBytesByUser.TryGetValue(userId, out long scopedUsedBytes))
        {
            long adjustedScopedUsedBytes = Math.Max(0, scopedUsedBytes - safeBytes);
            _usedBytesByUser[userId] = adjustedScopedUsedBytes;
            _cache.Set(cacheKey, adjustedScopedUsedBytes, UsedBytesCacheDuration);
            return;
        }

        if (_cache.TryGetValue(cacheKey, out long cachedUsedBytes))
        {
            _cache.Set(cacheKey, Math.Max(0, cachedUsedBytes - safeBytes), UsedBytesCacheDuration);
        }
    }

    private static string GetUsedBytesCacheKey(Guid userId) => $"user-storage-quota:used:{userId:N}";
}
