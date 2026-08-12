// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;

namespace Cotton.Server.Handlers.Files
{
    public record UpdateFileMetadataResult(
        UpdateFileMetadataStatus Status,
        NodeFileManifestDto? File = null,
        string? Error = null);
}
