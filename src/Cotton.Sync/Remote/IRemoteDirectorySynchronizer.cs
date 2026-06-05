// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Shared.Contracts.Nodes;

namespace Cotton.Sync.Remote;

/// <summary>
/// Performs remote directory mutations for the synchronization engine.
/// </summary>
public interface IRemoteDirectorySynchronizer
{
    /// <summary>
    /// Creates a remote child directory under the specified parent node.
    /// </summary>
    Task<NodeDto> CreateDirectoryAsync(
        Guid parentNodeId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a remote directory entry.
    /// </summary>
    Task DeleteDirectoryAsync(
        Guid nodeId,
        bool skipTrash = false,
        CancellationToken cancellationToken = default);
}
