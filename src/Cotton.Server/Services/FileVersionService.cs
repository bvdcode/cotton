// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Helpers;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

/// <summary>
/// Manages file-version lineages and restoration without introducing a separate version table.
/// </summary>
/// <remarks>
/// Historical versions are ordinary <see cref="NodeFile"/> rows linked by <c>OriginalNodeFileId</c>.
/// That keeps ownership, storage cleanup, quota accounting, and download authorization on the same
/// domain model as regular files. The first historical row is treated as the immutable original.
/// </remarks>
public sealed class FileVersionService(
    CottonDbContext _dbContext,
    NodeFileHistoryService _history,
    FileVersionRetentionService _retention,
    FileVersionStorageService _storage,
    UserStorageQuotaService _quota,
    ILogger<FileVersionService> _logger)
{
    private const int VersionDownloadTokenLength = 16;

    /// <summary>
    /// Indicates whether the file row is a historical version rather than the current visible file.
    /// </summary>
    public static bool IsHistoricalVersion(NodeFile file)
        => file.OriginalNodeFileId != Guid.Empty && file.Id != file.OriginalNodeFileId;

    /// <summary>
    /// Lists the current file and its historical versions in display order.
    /// </summary>
    public async Task<IReadOnlyList<FileVersionDto>> ListVersionsAsync(
        Guid userId,
        Guid nodeFileId,
        CancellationToken ct = default)
    {
        NodeFile current = await LoadCurrentFileOrThrowAsync(userId, nodeFileId, tracking: false, ct);
        List<NodeFile> historicalVersions = await LoadHistoricalVersionsAsync(userId, GetLineageId(current), tracking: false, ct);
        return BuildVersionDtos(current, historicalVersions);
    }

    /// <summary>
    /// Captures the current manifest as a historical version, then points the visible file at a new manifest.
    /// </summary>
    public Task<FileVersionCaptureResult> CaptureAndUpdateManifestAsync(
        NodeFile nodeFile,
        Guid newFileManifestId,
        Guid userId,
        CancellationToken ct = default)
    {
        return CaptureAndUpdateManifestAsync(
            nodeFile,
            newFileManifestId,
            userId,
            protectedVersionIds: null,
            ct: ct);
    }

    private async Task<FileVersionCaptureResult> CaptureAndUpdateManifestAsync(
        NodeFile nodeFile,
        Guid newFileManifestId,
        Guid userId,
        IReadOnlySet<Guid>? protectedVersionIds,
        CancellationToken ct)
    {
        bool captured = await _history.SaveVersionAndUpdateManifestAsync(
            nodeFile,
            newFileManifestId,
            userId,
            ct);
        if (!captured)
        {
            return FileVersionCaptureResult.Empty;
        }

        await _dbContext.SaveChangesAsync(ct);

        long removedBytes = await _retention.ApplyAsync(userId, GetLineageId(nodeFile), protectedVersionIds, ct);
        return new FileVersionCaptureResult(Captured: true, RemovedBytes: removedBytes);
    }

    /// <summary>
    /// Restores a historical version by first preserving the current state as the next version.
    /// </summary>
    public async Task<NodeFileManifestDto> RestoreVersionAsync(
        Guid userId,
        Guid nodeFileId,
        Guid versionId,
        CancellationToken ct = default)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        NodeFile current = await LoadCurrentFileOrThrowAsync(userId, nodeFileId, tracking: true, ct);
        await LayoutLocks.AcquireForLayoutAsync(_dbContext, current.Node.LayoutId, ct);

        NodeFile version = await LoadHistoricalVersionOrThrowAsync(userId, GetLineageId(current), versionId, tracking: true, ct);
        long addedBytes = await _quota.EnsureCanChangeFileManifestAsync(userId, current.Id, version.FileManifestId, ct);

        FileVersionCaptureResult capture = await CaptureAndUpdateManifestAsync(
            current,
            version.FileManifestId,
            userId,
            protectedVersionIds: new HashSet<Guid> { version.Id },
            ct: ct);
        current.FileManifest = version.FileManifest;

        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _quota.RecordLogicalBytesAdded(userId, addedBytes);
        if (capture.RemovedBytes > 0)
        {
            _quota.RecordLogicalBytesRemoved(userId, capture.RemovedBytes);
        }

        _logger.LogInformation(
            "User {UserId} restored file {NodeFileId} to version {VersionId}.",
            userId,
            nodeFileId,
            versionId);

        return current.Adapt<NodeFileManifestDto>();
    }

    /// <summary>
    /// Deletes a user-selected historical version while keeping the original version protected.
    /// </summary>
    public async Task DeleteVersionAsync(
        Guid userId,
        Guid nodeFileId,
        Guid versionId,
        CancellationToken ct = default)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        NodeFile current = await LoadCurrentFileOrThrowAsync(userId, nodeFileId, tracking: true, ct);
        await LayoutLocks.AcquireForLayoutAsync(_dbContext, current.Node.LayoutId, ct);

        Guid lineageId = GetLineageId(current);
        List<NodeFile> historicalVersions = await LoadHistoricalVersionsAsync(userId, lineageId, tracking: true, ct);
        NodeFile version = historicalVersions.SingleOrDefault(x => x.Id == versionId)
            ?? throw new EntityNotFoundException<NodeFile>();

        long removedBytes = await DeleteHistoricalVersionAsync(userId, historicalVersions, version, ct);
        await tx.CommitAsync(ct);
        _quota.RecordLogicalBytesRemoved(userId, removedBytes);

        _logger.LogInformation(
            "User {UserId} deleted version {VersionId} of file {NodeFileId}.",
            userId,
            versionId,
            nodeFileId);
    }

    /// <summary>
    /// Deletes a historical version by version identifier regardless of its current file entry.
    /// </summary>
    public async Task DeleteHistoricalVersionAsync(
        Guid userId,
        Guid versionId,
        CancellationToken ct = default)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        NodeFile version = await LoadHistoricalVersionByIdOrThrowAsync(userId, versionId, tracking: true, ct);
        await LayoutLocks.AcquireForLayoutAsync(_dbContext, version.Node.LayoutId, ct);

        List<NodeFile> historicalVersions = await LoadHistoricalVersionsAsync(
            userId,
            version.OriginalNodeFileId,
            tracking: true,
            ct);
        long removedBytes = await DeleteHistoricalVersionAsync(userId, historicalVersions, version, ct);
        await tx.CommitAsync(ct);
        _quota.RecordLogicalBytesRemoved(userId, removedBytes);

        _logger.LogInformation(
            "User {UserId} deleted historical file version {VersionId}.",
            userId,
            versionId);
    }

    /// <summary>
    /// Creates a temporary download link for a historical version.
    /// </summary>
    public async Task<string> CreateVersionDownloadLinkAsync(
        Guid userId,
        Guid nodeFileId,
        Guid versionId,
        int expireAfterMinutes,
        CancellationToken ct = default)
    {
        const int maxExpireMinutes = 60 * 24 * 365;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expireAfterMinutes, maxExpireMinutes, nameof(expireAfterMinutes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expireAfterMinutes, nameof(expireAfterMinutes));

        NodeFile current = await LoadCurrentFileOrThrowAsync(userId, nodeFileId, tracking: false, ct);
        NodeFile version = await LoadHistoricalVersionOrThrowAsync(userId, GetLineageId(current), versionId, tracking: false, ct);

        DownloadToken token = new()
        {
            FileName = version.Name,
            DeleteAfterUse = false,
            CreatedByUserId = userId,
            NodeFileId = version.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expireAfterMinutes),
            Token = StringHelpers.CreateRandomString(VersionDownloadTokenLength),
        };

        await _dbContext.DownloadTokens.AddAsync(token, ct);
        await _dbContext.SaveChangesAsync(ct);

        return Routes.V1.Files + $"/{version.Id}/download?token={token.Token}";
    }

    /// <summary>
    /// Deletes all historical versions that belong to a file lineage.
    /// </summary>
    public async Task<long> DeleteLineageVersionsAsync(
        Guid userId,
        Guid nodeFileId,
        CancellationToken ct = default)
    {
        NodeFile current = await LoadAnyOwnedFileOrThrowAsync(userId, nodeFileId, tracking: true, ct);
        Guid lineageId = GetLineageId(current);
        List<NodeFile> historicalVersions = await LoadHistoricalVersionsAsync(userId, lineageId, tracking: true, ct);
        if (historicalVersions.Count == 0)
        {
            return 0;
        }

        return await _storage.DeleteHistoricalVersionsAsync(userId, historicalVersions, ct);
    }

    /// <summary>
    /// Deletes historical versions for every current file in a folder subtree.
    /// </summary>
    public async Task<long> DeleteLineageVersionsForCurrentFilesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> nodeFileIds,
        CancellationToken ct = default)
    {
        if (nodeFileIds.Count == 0)
        {
            return 0;
        }

        Guid[] lineageIds = await _dbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.OwnerId == userId && nodeFileIds.Contains(x.Id))
            .Select(x => x.OriginalNodeFileId == Guid.Empty ? x.Id : x.OriginalNodeFileId)
            .Distinct()
            .ToArrayAsync(ct);
        if (lineageIds.Length == 0)
        {
            return 0;
        }

        List<NodeFile> historicalVersions = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .Where(x => x.OwnerId == userId
                && lineageIds.Contains(x.OriginalNodeFileId)
                && x.Id != x.OriginalNodeFileId)
            .ToListAsync(ct);

        return await _storage.DeleteHistoricalVersionsAsync(userId, historicalVersions, ct);
    }

    /// <summary>
    /// Checks whether any files inside the selected nodes still have historical versions.
    /// </summary>
    public Task<bool> ContainsHistoricalVersionsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> nodeIds,
        CancellationToken ct = default)
    {
        if (nodeIds.Count == 0)
        {
            return Task.FromResult(false);
        }

        return _dbContext.NodeFiles
            .AsNoTracking()
            .AnyAsync(x => x.OwnerId == userId
                && nodeIds.Contains(x.NodeId)
                && x.OriginalNodeFileId != Guid.Empty
                && x.Id != x.OriginalNodeFileId, ct);
    }

    private async Task<NodeFile> LoadCurrentFileOrThrowAsync(
        Guid userId,
        Guid nodeFileId,
        bool tracking,
        CancellationToken ct)
    {
        IQueryable<NodeFile> query = _dbContext.NodeFiles
            .Include(x => x.Node)
            .Include(x => x.FileManifest)
            .Where(x => x.Id == nodeFileId
                && x.OwnerId == userId
                && x.Node.Type == NodeType.Default);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException<NodeFile>();
    }

    private async Task<NodeFile> LoadAnyOwnedFileOrThrowAsync(
        Guid userId,
        Guid nodeFileId,
        bool tracking,
        CancellationToken ct)
    {
        IQueryable<NodeFile> query = _dbContext.NodeFiles
            .Include(x => x.Node)
            .Include(x => x.FileManifest)
            .Where(x => x.Id == nodeFileId && x.OwnerId == userId);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException<NodeFile>();
    }

    private async Task<NodeFile> LoadHistoricalVersionOrThrowAsync(
        Guid userId,
        Guid lineageId,
        Guid versionId,
        bool tracking,
        CancellationToken ct)
    {
        IQueryable<NodeFile> query = _dbContext.NodeFiles
            .Include(x => x.Node)
            .Include(x => x.FileManifest)
            .Where(x => x.Id == versionId
                && x.OwnerId == userId
                && x.OriginalNodeFileId == lineageId
                && x.Id != lineageId);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException<NodeFile>();
    }

    private async Task<NodeFile> LoadHistoricalVersionByIdOrThrowAsync(
        Guid userId,
        Guid versionId,
        bool tracking,
        CancellationToken ct)
    {
        IQueryable<NodeFile> query = _dbContext.NodeFiles
            .Include(x => x.Node)
            .Include(x => x.FileManifest)
            .Where(x => x.Id == versionId
                && x.OwnerId == userId
                && x.OriginalNodeFileId != Guid.Empty
                && x.Id != x.OriginalNodeFileId);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException<NodeFile>();
    }

    private async Task<List<NodeFile>> LoadHistoricalVersionsAsync(
        Guid userId,
        Guid lineageId,
        bool tracking,
        CancellationToken ct)
    {
        IQueryable<NodeFile> query = _dbContext.NodeFiles
            .Include(x => x.Node)
            .Include(x => x.FileManifest)
            .Where(x => x.OwnerId == userId
                && x.OriginalNodeFileId == lineageId
                && x.Id != lineageId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.ToListAsync(ct);
    }

    private async Task<long> DeleteHistoricalVersionAsync(
        Guid userId,
        IReadOnlyList<NodeFile> historicalVersions,
        NodeFile version,
        CancellationToken ct)
    {
        if (historicalVersions.Count == 0 || historicalVersions[0].Id == version.Id)
        {
            throw new BadRequestException<NodeFile>("The original file version cannot be deleted.");
        }

        return await _storage.DeleteHistoricalVersionsAsync(userId, [version], ct);
    }

    private static IReadOnlyList<FileVersionDto> BuildVersionDtos(
        NodeFile current,
        IReadOnlyList<NodeFile> historicalVersions)
    {
        var versions = new List<FileVersionDto>(historicalVersions.Count + 1);

        for (int index = 0; index < historicalVersions.Count; index++)
        {
            NodeFile version = historicalVersions[index];
            versions.Add(ToVersionDto(
                version,
                current.Id,
                versionNumber: index + 1,
                isCurrent: false,
                isOriginal: index == 0,
                canDelete: index != 0));
        }

        versions.Add(ToVersionDto(
            current,
            current.Id,
            versionNumber: historicalVersions.Count + 1,
            isCurrent: true,
            isOriginal: historicalVersions.Count == 0,
            canDelete: false));

        return [.. versions.OrderByDescending(x => x.VersionNumber)];
    }

    private static FileVersionDto ToVersionDto(
        NodeFile version,
        Guid currentNodeFileId,
        int versionNumber,
        bool isCurrent,
        bool isOriginal,
        bool canDelete)
    {
        return new FileVersionDto
        {
            Id = version.Id,
            NodeFileId = currentNodeFileId,
            FileManifestId = version.FileManifestId,
            Name = version.Name,
            ContentType = version.FileManifest.ContentType,
            SizeBytes = version.FileManifest.SizeBytes,
            CreatedAt = version.CreatedAt,
            VersionNumber = versionNumber,
            IsCurrent = isCurrent,
            IsOriginal = isOriginal,
            CanDelete = canDelete,
        };
    }

    // The current file starts a lineage with an empty OriginalNodeFileId. Historical rows always point
    // back to that first visible file id, so this helper normalizes both cases before querying versions.
    private static Guid GetLineageId(NodeFile file)
        => file.OriginalNodeFileId == Guid.Empty ? file.Id : file.OriginalNodeFileId;
}
