// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Shared.Contracts.Nodes;

namespace Cotton.Sync.Remote;

/// <summary>
/// Represents a crawled remote subtree.
/// </summary>
public sealed class RemoteTreeSnapshot
{
    /// <summary>
    /// Gets or sets the root node used for the crawl.
    /// </summary>
    public NodeDto RootNode { get; set; } = new();

    /// <summary>
    /// Gets or sets child directories discovered under the root.
    /// </summary>
    public List<RemoteDirectorySnapshot> Directories { get; set; } = [];

    /// <summary>
    /// Gets or sets files discovered under the root.
    /// </summary>
    public List<RemoteFileSnapshot> Files { get; set; } = [];
}
