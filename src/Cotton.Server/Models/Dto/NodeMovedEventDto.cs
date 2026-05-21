// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Payload of the realtime "NodeMoved" event. Carries both source and target
    /// parent IDs so clients viewing either folder can invalidate their cache —
    /// `Node.ParentId` alone reveals only the new location.
    /// </summary>
    public record NodeMovedEventDto(
        NodeDto Node,
        Guid OldParentId,
        Guid NewParentId);
}
