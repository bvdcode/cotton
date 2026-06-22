// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;

namespace Cotton.Topology.Abstractions
{
    /// <summary>
    /// Creates and retrieves user layout roots and low-level topology records.
    /// </summary>
    public interface ILayoutService
    {
        /// <summary>
        /// Creates an isolated trash container node for a deleted item.
        /// </summary>
        Task<Node> CreateTrashItemAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Finds a stored chunk by hash.
        /// </summary>
        Task<Chunk?> FindChunkAsync(byte[] hash, CancellationToken ct = default);

        /// <summary>
        /// Returns the active layout for a user, creating one when needed.
        /// </summary>
        Task<Layout> GetOrCreateLatestUserLayoutAsync(Guid ownerId, CancellationToken ct = default);

        /// <summary>
        /// Returns the root node for a typed tree, creating one when needed.
        /// </summary>
        Task<Node> GetOrCreateRootNodeAsync(Guid layoutId, Guid ownerId, NodeType nodeType, CancellationToken ct = default);

        /// <summary>
        /// Returns the user trash root node.
        /// </summary>
        Task<Node> GetUserTrashRootAsync(Guid ownerId, CancellationToken ct = default);
    }
}
