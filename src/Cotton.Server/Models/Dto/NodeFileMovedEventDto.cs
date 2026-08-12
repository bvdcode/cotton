// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;

namespace Cotton.Server.Models.Dto
{
    public record NodeFileMovedEventDto(
        NodeFileManifestDto File,
        Guid OldParentId,
        Guid NewParentId);
}
