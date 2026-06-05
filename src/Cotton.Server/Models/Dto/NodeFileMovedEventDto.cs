// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Payload of the realtime "FileMoved" event. Carries both source and target
    /// parent IDs so clients viewing either folder can invalidate their cache —
    /// `File.NodeId` alone reveals only the new location.
    /// </summary>
    public record NodeFileMovedEventDto(
        NodeFileManifestDto File,
        Guid OldParentId,
        Guid NewParentId);
}
