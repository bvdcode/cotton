// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Server.Hubs;
using Cotton.Server.Models.Dto;
using Mapster;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Cotton.Database.Models;

namespace Cotton.Server.Services
{
    public class EventNotificationService(
        IHubContext<EventHub> _hubContext,
        CottonDbContext _dbContext) : IEventNotificationService
    {
        public async Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default)
        {
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

            if (nodeFile is not null)
            {
                NodeFileManifestDto dto = nodeFile.Adapt<NodeFileManifestDto>();
                await NotifyFileCreatedAsync(dto, ct);
            }
        }

        public Task NotifyFileCreatedAsync(NodeFileManifestDto file, CancellationToken ct = default)
        {
            return _hubContext.Clients.User(file.OwnerId.ToString()).SendAsync("FileCreated", file, ct);
        }

        public async Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default)
        {
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

            if (nodeFile is not null)
            {
                NodeFileManifestDto dto = nodeFile.Adapt<NodeFileManifestDto>();
                await NotifyFileUpdatedAsync(dto, ct);
            }
        }

        public Task NotifyFileUpdatedAsync(NodeFileManifestDto file, CancellationToken ct = default)
        {
            return _hubContext.Clients.User(file.OwnerId.ToString()).SendAsync("FileUpdated", file, ct);
        }

        public async Task NotifyFileDeletedAsync(
            Guid userId,
            Guid nodeFileId,
            Guid? parentNodeId,
            CancellationToken ct = default)
        {
            var payload = new NodeFileDeletedEventDto(nodeFileId, parentNodeId);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileDeleted", payload, ct);
        }

        public async Task NotifyFileMovedAsync(Guid nodeFileId, Guid oldParentId, CancellationToken ct = default)
        {
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

            if (nodeFile is not null)
            {
                NodeFileManifestDto dto = nodeFile.Adapt<NodeFileManifestDto>();
                var payload = new NodeFileMovedEventDto(dto, oldParentId, nodeFile.NodeId);
                await _hubContext.Clients.User(nodeFile.OwnerId.ToString()).SendAsync("FileMoved", payload, ct);
            }
        }

        public async Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default)
        {
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

            if (nodeFile is not null)
            {
                NodeFileManifestDto dto = nodeFile.Adapt<NodeFileManifestDto>();
                await NotifyFileRenamedAsync(dto, ct);
            }
        }

        public Task NotifyFileRenamedAsync(NodeFileManifestDto file, CancellationToken ct = default)
        {
            return _hubContext.Clients.User(file.OwnerId.ToString()).SendAsync("FileRenamed", file, ct);
        }

        public Task NotifyFileRestoredAsync(
            Guid userId,
            Guid nodeFileId,
            NodeFileManifestDto? file,
            CancellationToken ct = default)
        {
            object payload = file is not null
                ? file
                : new { id = nodeFileId };
            return _hubContext.Clients.User(userId.ToString()).SendAsync("FileRestored", payload, ct);
        }

        public async Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default)
        {
            Node? node = await _dbContext.Nodes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

            if (node is not null)
            {
                NodeDto dto = node.Adapt<NodeDto>();
                await NotifyNodeCreatedAsync(node.OwnerId, dto, ct);
            }
        }

        public Task NotifyNodeCreatedAsync(Guid userId, NodeDto node, CancellationToken ct = default)
        {
            return _hubContext.Clients.User(userId.ToString()).SendAsync("NodeCreated", node, ct);
        }

        public async Task NotifyNodeDeletedAsync(
            Guid userId,
            Guid nodeId,
            Guid? parentNodeId,
            CancellationToken ct = default)
        {
            var payload = new NodeDeletedEventDto(nodeId, parentNodeId);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NodeDeleted", payload, ct);
        }

        public async Task NotifyNodeMovedAsync(Guid nodeId, Guid oldParentId, CancellationToken ct = default)
        {
            Node? node = await _dbContext.Nodes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

            if (node is not null && node.ParentId.HasValue)
            {
                NodeDto dto = node.Adapt<NodeDto>();
                var payload = new NodeMovedEventDto(dto, oldParentId, node.ParentId.Value);
                await _hubContext.Clients.User(node.OwnerId.ToString()).SendAsync("NodeMoved", payload, ct);
            }
        }

        public async Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default)
        {
            Node? node = await _dbContext.Nodes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

            if (node is not null)
            {
                NodeDto dto = node.Adapt<NodeDto>();
                await NotifyNodeRenamedAsync(node.OwnerId, dto, ct);
            }
        }

        public Task NotifyNodeRenamedAsync(Guid userId, NodeDto node, CancellationToken ct = default)
        {
            return _hubContext.Clients.User(userId.ToString()).SendAsync("NodeRenamed", node, ct);
        }

        public Task NotifyNodeMetadataUpdatedAsync(Guid userId, NodeDto node, CancellationToken ct = default)
        {
            return _hubContext.Clients.User(userId.ToString()).SendAsync("NodeMetadataUpdated", node, ct);
        }

        public Task NotifyNodeRestoredAsync(
            Guid userId,
            Guid nodeId,
            NodeDto? node,
            CancellationToken ct = default)
        {
            object payload = node is not null
                ? node
                : new { id = nodeId };
            return _hubContext.Clients.User(userId.ToString()).SendAsync("NodeRestored", payload, ct);
        }
    }
}
