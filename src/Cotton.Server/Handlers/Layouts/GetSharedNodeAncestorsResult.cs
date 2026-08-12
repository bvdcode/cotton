// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;

namespace Cotton.Server.Handlers.Layouts
{
    public record GetSharedNodeAncestorsResult(
        GetSharedNodeAncestorsStatus Status,
        IReadOnlyList<NodeDto>? Ancestors = null,
        string? Error = null);
}
