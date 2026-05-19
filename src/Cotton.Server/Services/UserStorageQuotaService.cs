// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

public class UserStorageQuotaService(
    CottonDbContext _dbContext,
    SettingsProvider _settings)
{
    private readonly Dictionary<Guid, long> _usedBytesByUser = [];

    public async Task<long> GetUsedBytesAsync(Guid userId, CancellationToken ct = default)
    {
        if (_usedBytesByUser.TryGetValue(userId, out long cachedUsedBytes))
        {
            return cachedUsedBytes;
        }

        long? usedBytes = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .SumAsync(x => (long?)x.FileManifest.SizeBytes, ct);

        long resolvedUsedBytes = usedBytes ?? 0;
        _usedBytesByUser[userId] = resolvedUsedBytes;
        return resolvedUsedBytes;
    }

    public async Task EnsureCanAddFileReferenceAsync(
        Guid userId,
        Guid fileManifestId,
        CancellationToken ct = default)
    {
        long additionalBytes = await _dbContext.FileManifests
            .AsNoTracking()
            .Where(x => x.Id == fileManifestId)
            .Select(x => x.SizeBytes)
            .SingleAsync(ct);

        await EnsureCanAddLogicalBytesAsync(userId, additionalBytes, ct);
    }

    public async Task EnsureCanChangeFileManifestAsync(
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
            return;
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

        await EnsureCanAddLogicalBytesAsync(userId, additionalBytes, ct);
    }

    private async Task EnsureCanAddLogicalBytesAsync(
        Guid userId,
        long additionalBytes,
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

        _usedBytesByUser[userId] = usedBytes + safeAdditionalBytes;
    }
}
