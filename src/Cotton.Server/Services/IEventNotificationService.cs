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
    public interface IEventNotificationService
    {
        Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default);

        Task NotifyFileCreatedAsync(NodeFileManifestDto file, CancellationToken ct = default);

        Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default);

        Task NotifyFileUpdatedAsync(NodeFileManifestDto file, CancellationToken ct = default);

        Task NotifyFileDeletedAsync(Guid userId, Guid nodeFileId, Guid? parentNodeId, CancellationToken ct = default);

        Task NotifyFileMovedAsync(Guid nodeFileId, Guid oldParentId, CancellationToken ct = default);

        Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default);

        Task NotifyFileRenamedAsync(NodeFileManifestDto file, CancellationToken ct = default);

        Task NotifyFileRestoredAsync(
            Guid userId,
            Guid nodeFileId,
            NodeFileManifestDto? file,
            CancellationToken ct = default);

        Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default);

        Task NotifyNodeCreatedAsync(Guid userId, NodeDto node, CancellationToken ct = default);

        Task NotifyNodeDeletedAsync(Guid userId, Guid nodeId, Guid? parentNodeId, CancellationToken ct = default);

        Task NotifyNodeMovedAsync(Guid nodeId, Guid oldParentId, CancellationToken ct = default);

        Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default);

        Task NotifyNodeRenamedAsync(Guid userId, NodeDto node, CancellationToken ct = default);

        Task NotifyNodeMetadataUpdatedAsync(Guid userId, NodeDto node, CancellationToken ct = default);

        Task NotifyNodeRestoredAsync(
            Guid userId,
            Guid nodeId,
            NodeDto? node,
            CancellationToken ct = default);
    }
}
