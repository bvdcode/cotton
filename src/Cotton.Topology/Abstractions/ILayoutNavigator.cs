// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;

namespace Cotton.Topology.Abstractions;

/// <summary>
/// Resolves node paths inside a user's typed layout tree.
/// </summary>
public interface ILayoutNavigator
{
    /// <summary>Returns the active layout and root node for the requested tree type.</summary>
    Task<(Layout Layout, Node Root)> GetLayoutAndRootAsync(Guid userId, NodeType nodeType, CancellationToken ct = default);
    /// <summary>Resolves a slash-separated node path relative to the requested root.</summary>
    Task<Node?> ResolveNodeByPathAsync(Guid userId, string? path, NodeType nodeType, CancellationToken ct = default);
    /// <summary>Resolves the parent node and final resource name for a path that may not exist yet.</summary>
    Task<(Node Parent, string ResourceName)?> ResolveParentAndNameAsync(Guid userId, string path, NodeType nodeType, CancellationToken ct = default);
    /// <summary>Builds the display path from the root node to a child node.</summary>
    Task<string?> GetNodePathFromRootAsync(Guid userId, Guid nodeId, NodeType nodeType, CancellationToken ct = default);
}
