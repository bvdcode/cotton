// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;

namespace Cotton.Topology.Abstractions
{
    public interface ILayoutService
    {
        Task<Node> CreateTrashItemAsync(Guid userId, CancellationToken ct = default);

        Task<Chunk?> FindChunkAsync(byte[] hash, CancellationToken ct = default);

        Task<Layout> GetOrCreateLatestUserLayoutAsync(Guid ownerId, CancellationToken ct = default);

        Task<Node> GetOrCreateRootNodeAsync(Guid layoutId, Guid ownerId, NodeType nodeType, CancellationToken ct = default);

        Task<Node> GetUserTrashRootAsync(Guid ownerId, CancellationToken ct = default);
    }
}
