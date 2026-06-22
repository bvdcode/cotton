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
    /// <summary>
    /// Coordinates event notification.
    /// </summary>
    public class EventNotificationService(
        IHubContext<EventHub> _hubContext,
        CottonDbContext _dbContext) : IEventNotificationService
    {
        /// <summary>
        /// Notifies connected clients that file created occurred.
        /// </summary>
        public async Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default)
        {
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

            if (nodeFile is not null)
            {
                NodeFileManifestDto dto = nodeFile.Adapt<NodeFileManifestDto>();
                await _hubContext.Clients.User(nodeFile.OwnerId.ToString()).SendAsync("FileCreated", dto, ct);
            }
        }

        /// <summary>
        /// Notifies connected clients that file updated occurred.
        /// </summary>
        public async Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default)
        {
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

            if (nodeFile is not null)
            {
                NodeFileManifestDto dto = nodeFile.Adapt<NodeFileManifestDto>();
                await _hubContext.Clients.User(nodeFile.OwnerId.ToString()).SendAsync("FileUpdated", dto, ct);
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
            var payload = new NodeFileDeletedEventDto(nodeFileId, parentNodeId);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("FileDeleted", payload, ct);
        }

        /// <summary>
        /// Notifies connected clients that file moved occurred.
        /// </summary>
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

        /// <summary>
        /// Notifies connected clients that file renamed occurred.
        /// </summary>
        public async Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default)
        {
            NodeFile? nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeFileId, ct);

            if (nodeFile is not null)
            {
                NodeFileManifestDto dto = nodeFile.Adapt<NodeFileManifestDto>();
                await _hubContext.Clients.User(nodeFile.OwnerId.ToString()).SendAsync("FileRenamed", dto, ct);
            }
        }

        /// <summary>
        /// Notifies connected clients that node created occurred.
        /// </summary>
        public async Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default)
        {
            Node? node = await _dbContext.Nodes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

            if (node is not null)
            {
                NodeDto dto = node.Adapt<NodeDto>();
                await _hubContext.Clients.User(node.OwnerId.ToString()).SendAsync("NodeCreated", dto, ct);
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
            var payload = new NodeDeletedEventDto(nodeId, parentNodeId);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NodeDeleted", payload, ct);
        }

        /// <summary>
        /// Notifies connected clients that node moved occurred.
        /// </summary>
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

        /// <summary>
        /// Notifies connected clients that node renamed occurred.
        /// </summary>
        public async Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default)
        {
            Node? node = await _dbContext.Nodes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nodeId, ct);

            if (node is not null)
            {
                NodeDto dto = node.Adapt<NodeDto>();
                await _hubContext.Clients.User(node.OwnerId.ToString()).SendAsync("NodeRenamed", dto, ct);
            }
        }
    }
}
