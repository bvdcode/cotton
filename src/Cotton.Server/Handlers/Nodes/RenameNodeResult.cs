// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Server.Handlers.Nodes
{
    public record RenameNodeResult(
        RenameNodeStatus Status,
        NodeDto? Node = null,
        string? Error = null);
}
