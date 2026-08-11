// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Represents the outcome of updating node metadata.
    /// </summary>
    public record UpdateNodeMetadataResult(
        UpdateNodeMetadataStatus Status,
        NodeDto? Node = null,
        string? Error = null);
}
