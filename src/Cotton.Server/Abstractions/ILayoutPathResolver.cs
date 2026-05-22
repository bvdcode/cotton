// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;

namespace Cotton.Server.Abstractions;

/// <summary>
/// Defines the layout path resolver contract used by the server runtime.
/// </summary>
public interface ILayoutPathResolver
{
    /// <summary>
    /// Gets layout and root.
    /// </summary>
    Task<(Layout Layout, Node Root)> GetLayoutAndRootAsync(Guid userId, NodeType nodeType, CancellationToken ct = default);
    /// <summary>
    /// Resolves node by path.
    /// </summary>
    Task<Node?> ResolveNodeByPathAsync(Guid userId, string? path, NodeType nodeType, CancellationToken ct = default);
}
