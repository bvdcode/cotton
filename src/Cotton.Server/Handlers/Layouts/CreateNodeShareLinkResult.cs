// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Represents the outcome of creating a node share link.
    /// </summary>
    public record CreateNodeShareLinkResult(
        CreateNodeShareLinkStatus Status,
        string? Link = null);
}
