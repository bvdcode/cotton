// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;

namespace Cotton.Sdk.Nodes;

/// <summary>
/// Provides node and folder operations used by synchronization clients.
/// </summary>
public interface ICottonNodeClient
{
    /// <summary>
    /// Resolves the latest layout root or a descendant path.
    /// </summary>
    Task<NodeDto> ResolveAsync(string? path = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a node by identifier.
    /// </summary>
    Task<NodeDto> GetAsync(Guid nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one page of child nodes and files.
    /// </summary>
    Task<NodeContentDto> GetChildrenAsync(Guid nodeId, int page = 1, int pageSize = 100, int depth = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a child node under the specified parent.
    /// </summary>
    Task<NodeDto> CreateAsync(Guid parentId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a node under a different parent node.
    /// </summary>
    Task<NodeDto> MoveAsync(Guid nodeId, Guid parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a node.
    /// </summary>
    Task<NodeDto> RenameAsync(Guid nodeId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges metadata into a node.
    /// </summary>
    Task<NodeDto> UpdateMetadataAsync(Guid nodeId, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a node.
    /// </summary>
    Task DeleteAsync(Guid nodeId, bool skipTrash = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a trashed node.
    /// </summary>
    Task<NodeDto> RestoreAsync(Guid nodeId, RestoreItemRequestDto? request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets ancestor nodes for a node.
    /// </summary>
    Task<List<NodeDto>> GetAncestorsAsync(Guid nodeId, CancellationToken cancellationToken = default);
}
