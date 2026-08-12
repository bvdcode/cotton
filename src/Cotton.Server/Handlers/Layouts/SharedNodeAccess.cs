// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Represents a verified active shared node boundary.
    /// </summary>
    public record SharedNodeAccess(
        string Token,
        Guid NodeId,
        Guid CreatedByUserId,
        string Name,
        DateTime? ExpiresAt);
}
