// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Payload of the realtime "FileDeleted" event. Carries the containing
    /// folder ID so clients viewing that folder can invalidate precisely.
    /// </summary>
    public record NodeFileDeletedEventDto(
        Guid NodeFileId,
        Guid? ParentNodeId);
}
