// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

/// <summary>Records durable sync feed changes for remote synchronization clients.</summary>
public interface ISyncChangeRecorder
{
    /// <summary>Records a file snapshot change.</summary>
    Task RecordFileChangeAsync(
        SyncChangeKind kind,
        Guid nodeFileId,
        Guid? previousParentNodeId = null,
        CancellationToken ct = default);

    /// <summary>Records a file deletion change.</summary>
    Task RecordFileDeletedAsync(
        Guid userId,
        Guid nodeFileId,
        Guid? parentNodeId,
        CancellationToken ct = default);

    /// <summary>Records a folder snapshot change.</summary>
    Task RecordNodeChangeAsync(
        SyncChangeKind kind,
        Guid nodeId,
        Guid? previousParentNodeId = null,
        CancellationToken ct = default);

    /// <summary>Records a folder deletion change.</summary>
    Task RecordNodeDeletedAsync(
        Guid userId,
        Guid nodeId,
        Guid? parentNodeId,
        CancellationToken ct = default);
}

/// <summary>Persists ordered file-tree mutations for sync clients.</summary>
public sealed class SyncChangeRecorder(CottonDbContext _dbContext) : ISyncChangeRecorder
{
    /// <inheritdoc />
    public async Task RecordFileChangeAsync(
        SyncChangeKind kind,
        Guid nodeFileId,
        Guid? previousParentNodeId = null,
        CancellationToken ct = default)
    {
        NodeFile? nodeFile = await _dbContext.NodeFiles
            .Include(x => x.Node)
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);
        if (nodeFile is null)
        {
            return;
        }

        await AddAndSaveAsync(new SyncChange
        {
            OwnerId = nodeFile.OwnerId,
            Kind = kind,
            LayoutId = nodeFile.Node.LayoutId,
            NodeId = nodeFile.NodeId,
            NodeFileId = nodeFile.Id,
            ParentNodeId = nodeFile.NodeId,
            PreviousParentNodeId = previousParentNodeId,
            FileManifestId = nodeFile.FileManifestId,
            OriginalNodeFileId = nodeFile.OriginalNodeFileId,
            Name = nodeFile.Name,
            ContentHash = Hasher.ToHexStringHash(nodeFile.FileManifest.ProposedContentHash),
            ETag = "sha256-" + Hasher.ToHexStringHash(nodeFile.FileManifest.ProposedContentHash),
            SizeBytes = nodeFile.FileManifest.SizeBytes,
        }, ct);
    }

    /// <inheritdoc />
    public Task RecordFileDeletedAsync(
        Guid userId,
        Guid nodeFileId,
        Guid? parentNodeId,
        CancellationToken ct = default)
    {
        return AddAndSaveAsync(new SyncChange
        {
            OwnerId = userId,
            Kind = SyncChangeKind.FileDeleted,
            NodeId = parentNodeId,
            NodeFileId = nodeFileId,
            ParentNodeId = parentNodeId,
            PreviousParentNodeId = parentNodeId,
        }, ct);
    }

    /// <inheritdoc />
    public async Task RecordNodeChangeAsync(
        SyncChangeKind kind,
        Guid nodeId,
        Guid? previousParentNodeId = null,
        CancellationToken ct = default)
    {
        Node? node = await _dbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeId, ct);
        if (node is null)
        {
            return;
        }

        await AddAndSaveAsync(new SyncChange
        {
            OwnerId = node.OwnerId,
            Kind = kind,
            LayoutId = node.LayoutId,
            NodeId = node.Id,
            ParentNodeId = node.ParentId,
            PreviousParentNodeId = previousParentNodeId,
            Name = node.Name,
        }, ct);
    }

    /// <inheritdoc />
    public Task RecordNodeDeletedAsync(
        Guid userId,
        Guid nodeId,
        Guid? parentNodeId,
        CancellationToken ct = default)
    {
        return AddAndSaveAsync(new SyncChange
        {
            OwnerId = userId,
            Kind = SyncChangeKind.FolderDeleted,
            NodeId = nodeId,
            ParentNodeId = parentNodeId,
            PreviousParentNodeId = parentNodeId,
        }, ct);
    }

    private async Task AddAndSaveAsync(SyncChange change, CancellationToken ct)
    {
        await _dbContext.SyncChanges.AddAsync(change, ct);
        await _dbContext.SaveChangesAsync(ct);
    }
}
