// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Server.Handlers.Nodes
{
    /// <summary>
    /// Represents the outcome of creating a node.
    /// </summary>
    public record CreateNodeResult(
        CreateNodeStatus Status,
        NodeDto? Node = null,
        string? Error = null);
}
