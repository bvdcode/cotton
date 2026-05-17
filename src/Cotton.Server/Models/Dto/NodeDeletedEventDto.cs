// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Payload of the realtime "NodeDeleted" event. Carries the previous parent
    /// folder ID so clients viewing that folder can invalidate precisely.
    /// </summary>
    public record NodeDeletedEventDto(
        Guid NodeId,
        Guid? ParentNodeId);
}
