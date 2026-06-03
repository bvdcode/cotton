// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Hubs;
using Cotton.Server.Models.Dto;
using Mapster;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

/// <summary>
/// Defines the event notification service contract used by the server runtime.
/// </summary>
public interface IEventNotificationService
{
    /// <summary>
    /// Notifies connected clients that file created occurred.
    /// </summary>
    Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that file updated occurred.
    /// </summary>
    Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that file deleted occurred.
    /// </summary>
    Task NotifyFileDeletedAsync(Guid userId, Guid nodeFileId, Guid? parentNodeId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that file restored occurred.
    /// </summary>
    Task NotifyFileRestoredAsync(Guid nodeFileId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that file moved occurred.
    /// </summary>
    Task NotifyFileMovedAsync(Guid nodeFileId, Guid oldParentId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that file renamed occurred.
    /// </summary>
    Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that node created occurred.
    /// </summary>
    Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that node deleted occurred.
    /// </summary>
    Task NotifyNodeDeletedAsync(Guid userId, Guid nodeId, Guid? parentNodeId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that node restored occurred.
    /// </summary>
    Task NotifyNodeRestoredAsync(Guid nodeId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that node moved occurred.
    /// </summary>
    Task NotifyNodeMovedAsync(Guid nodeId, Guid oldParentId, CancellationToken ct = default);
    /// <summary>
    /// Notifies connected clients that node renamed occurred.
    /// </summary>
    Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default);
}

/// <summary>
/// Coordinates event notification.
/// </summary>
public class EventNotificationService(
    IHubContext<EventHub> _hubContext,
    CottonDbContext _dbContext,
    ISyncChangeRecorder _syncChanges,
    ILogger<EventNotificationService> _logger) : IEventNotificationService
{
    /// <summary>
    /// Notifies connected clients that file created occurred.
    /// </summary>
    public async Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default)
    {
        var nodeFile = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

        if (nodeFile is not null)
        {
            await _syncChanges.RecordFileChangeAsync(SyncChangeKind.FileCreated, nodeFileId, ct: ct);
            var dto = nodeFile.Adapt<NodeFileManifestDto>();
            await SendUserEventAsync(nodeFile.OwnerId, "FileCreated", dto, ct);
        }
    }

    /// <summary>
    /// Notifies connected clients that file updated occurred.
    /// </summary>
    public async Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default)
    {
        var nodeFile = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

        if (nodeFile is not null)
        {
            await _syncChanges.RecordFileChangeAsync(SyncChangeKind.FileContentUpdated, nodeFileId, ct: ct);
            var dto = nodeFile.Adapt<NodeFileManifestDto>();
            await SendUserEventAsync(nodeFile.OwnerId, "FileUpdated", dto, ct);
        }
    }

    /// <summary>
    /// Notifies connected clients that file deleted occurred.
    /// </summary>
    public async Task NotifyFileDeletedAsync(
        Guid userId,
        Guid nodeFileId,
        Guid? parentNodeId,
        CancellationToken ct = default)
    {
        await _syncChanges.RecordFileDeletedAsync(userId, nodeFileId, parentNodeId, ct);
        var payload = new NodeFileDeletedEventDto(nodeFileId, parentNodeId);
        await SendUserEventAsync(userId, "FileDeleted", payload, ct);
    }

    /// <summary>
    /// Notifies connected clients that file restored occurred.
    /// </summary>
    public async Task NotifyFileRestoredAsync(Guid nodeFileId, CancellationToken ct = default)
    {
        var nodeFile = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

        if (nodeFile is not null)
        {
            await _syncChanges.RecordFileChangeAsync(SyncChangeKind.FileRestored, nodeFileId, ct: ct);
            var dto = nodeFile.Adapt<NodeFileManifestDto>();
            await SendUserEventAsync(nodeFile.OwnerId, "FileRestored", dto, ct);
        }
    }

    /// <summary>
    /// Notifies connected clients that file moved occurred.
    /// </summary>
    public async Task NotifyFileMovedAsync(Guid nodeFileId, Guid oldParentId, CancellationToken ct = default)
    {
        var nodeFile = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

        if (nodeFile is not null)
        {
            await _syncChanges.RecordFileChangeAsync(SyncChangeKind.FileMoved, nodeFileId, oldParentId, ct);
            var dto = nodeFile.Adapt<NodeFileManifestDto>();
            var payload = new NodeFileMovedEventDto(dto, oldParentId, nodeFile.NodeId);
            await SendUserEventAsync(nodeFile.OwnerId, "FileMoved", payload, ct);
        }
    }

    /// <summary>
    /// Notifies connected clients that file renamed occurred.
    /// </summary>
    public async Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default)
    {
        var nodeFile = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

        if (nodeFile is not null)
        {
            await _syncChanges.RecordFileChangeAsync(SyncChangeKind.FileRenamed, nodeFileId, ct: ct);
            var dto = nodeFile.Adapt<NodeFileManifestDto>();
            await SendUserEventAsync(nodeFile.OwnerId, "FileRenamed", dto, ct);
        }
    }

    /// <summary>
    /// Notifies connected clients that node created occurred.
    /// </summary>
    public async Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _dbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

        if (node is not null)
        {
            await _syncChanges.RecordNodeChangeAsync(SyncChangeKind.FolderCreated, nodeId, ct: ct);
            var dto = node.Adapt<NodeDto>();
            await SendUserEventAsync(node.OwnerId, "NodeCreated", dto, ct);
        }
    }

    /// <summary>
    /// Notifies connected clients that node deleted occurred.
    /// </summary>
    public async Task NotifyNodeDeletedAsync(
        Guid userId,
        Guid nodeId,
        Guid? parentNodeId,
        CancellationToken ct = default)
    {
        await _syncChanges.RecordNodeDeletedAsync(userId, nodeId, parentNodeId, ct);
        var payload = new NodeDeletedEventDto(nodeId, parentNodeId);
        await SendUserEventAsync(userId, "NodeDeleted", payload, ct);
    }

    /// <summary>
    /// Notifies connected clients that node restored occurred.
    /// </summary>
    public async Task NotifyNodeRestoredAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _dbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

        if (node is not null)
        {
            await _syncChanges.RecordNodeChangeAsync(SyncChangeKind.FolderRestored, nodeId, ct: ct);
            var dto = node.Adapt<NodeDto>();
            await SendUserEventAsync(node.OwnerId, "NodeRestored", dto, ct);
        }
    }

    /// <summary>
    /// Notifies connected clients that node moved occurred.
    /// </summary>
    public async Task NotifyNodeMovedAsync(Guid nodeId, Guid oldParentId, CancellationToken ct = default)
    {
        var node = await _dbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

        if (node is not null && node.ParentId.HasValue)
        {
            await _syncChanges.RecordNodeChangeAsync(SyncChangeKind.FolderMoved, nodeId, oldParentId, ct);
            var dto = node.Adapt<NodeDto>();
            var payload = new NodeMovedEventDto(dto, oldParentId, node.ParentId.Value);
            await SendUserEventAsync(node.OwnerId, "NodeMoved", payload, ct);
        }
    }

    /// <summary>
    /// Notifies connected clients that node renamed occurred.
    /// </summary>
    public async Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _dbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

        if (node is not null)
        {
            await _syncChanges.RecordNodeChangeAsync(SyncChangeKind.FolderRenamed, nodeId, ct: ct);
            var dto = node.Adapt<NodeDto>();
            await SendUserEventAsync(node.OwnerId, "NodeRenamed", dto, ct);
        }
    }

    private async Task SendUserEventAsync(Guid userId, string eventName, object payload, CancellationToken ct)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync(eventName, payload, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send realtime event {EventName} to user {UserId}.",
                eventName,
                userId);
        }
    }
}
