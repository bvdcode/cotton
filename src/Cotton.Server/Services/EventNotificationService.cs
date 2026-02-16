// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Hubs;
using Cotton.Server.Models.Dto;
using Mapster;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

public interface IEventNotificationService
{
    Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default);
    Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default);
    Task NotifyFileDeletedAsync(Guid userId, Guid nodeFileId, CancellationToken ct = default);
    Task NotifyFileMovedAsync(Guid nodeFileId, CancellationToken ct = default);
    Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default);
    Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default);
    Task NotifyNodeDeletedAsync(Guid userId, Guid nodeId, CancellationToken ct = default);
    Task NotifyNodeMovedAsync(Guid nodeId, CancellationToken ct = default);
    Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default);
}

public class EventNotificationService(
    IHubContext<EventHub> _hubContext,
    CottonDbContext _dbContext) : IEventNotificationService
{
    public async Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default)
    {
        var nodeFile = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

        if (nodeFile is not null)
        {
            var dto = nodeFile.Adapt<NodeFileManifestDto>();
            await _hubContext.Clients.User(nodeFile.OwnerId.ToString()).SendAsync("FileCreated", dto, ct);
        }
    }

    public async Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default)
    {
        var nodeFile = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

        if (nodeFile is not null)
        {
            var dto = nodeFile.Adapt<NodeFileManifestDto>();
            await _hubContext.Clients.User(nodeFile.OwnerId.ToString()).SendAsync("FileUpdated", dto, ct);
        }
    }

    public async Task NotifyFileDeletedAsync(Guid userId, Guid nodeFileId, CancellationToken ct = default)
    {
        await _hubContext.Clients.User(userId.ToString()).SendAsync("FileDeleted", nodeFileId, ct);
    }

    public async Task NotifyFileMovedAsync(Guid nodeFileId, CancellationToken ct = default)
    {
        var nodeFile = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

        if (nodeFile is not null)
        {
            var dto = nodeFile.Adapt<NodeFileManifestDto>();
            await _hubContext.Clients.User(nodeFile.OwnerId.ToString()).SendAsync("FileMoved", dto, ct);
        }
    }

    public async Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default)
    {
        var nodeFile = await _dbContext.NodeFiles
            .Include(x => x.FileManifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

        if (nodeFile is not null)
        {
            var dto = nodeFile.Adapt<NodeFileManifestDto>();
            await _hubContext.Clients.User(nodeFile.OwnerId.ToString()).SendAsync("FileRenamed", dto, ct);
        }
    }

    public async Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _dbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

        if (node is not null)
        {
            var dto = node.Adapt<NodeDto>();
            await _hubContext.Clients.User(node.OwnerId.ToString()).SendAsync("NodeCreated", dto, ct);
        }
    }

    public async Task NotifyNodeDeletedAsync(Guid userId, Guid nodeId, CancellationToken ct = default)
    {
        await _hubContext.Clients.User(userId.ToString()).SendAsync("NodeDeleted", nodeId, ct);
    }

    public async Task NotifyNodeMovedAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _dbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

        if (node is not null)
        {
            var dto = node.Adapt<NodeDto>();
            await _hubContext.Clients.User(node.OwnerId.ToString()).SendAsync("NodeMoved", dto, ct);
        }
    }

    public async Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _dbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

        if (node is not null)
        {
            var dto = node.Adapt<NodeDto>();
            await _hubContext.Clients.User(node.OwnerId.ToString()).SendAsync("NodeRenamed", dto, ct);
        }
    }
}
