// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Represents the outcome of loading shared node children.
    /// </summary>
    public record GetSharedNodeChildrenResult(
        GetSharedNodeChildrenStatus Status,
        SharedNodeContentDto? Content = null,
        int TotalCount = 0);
}
