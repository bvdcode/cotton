// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;

namespace Cotton.Topology.Abstractions
{
    public interface ILayoutService
    {
        Task<Node> CreateTrashItemAsync(Guid userId);
        Task<Chunk?> FindChunkAsync(byte[] hash);
        Task<Layout> GetOrCreateLatestUserLayoutAsync(Guid ownerId);
        Task<Node> GetOrCreateRootNodeAsync(Guid layoutId, Guid ownerId, NodeType nodeType);
        Task<Node> GetUserTrashRootAsync(Guid ownerId);
    }
}