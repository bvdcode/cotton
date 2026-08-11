// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Represents the outcome of updating file content.
    /// </summary>
    public record UpdateFileContentResult(
        UpdateFileContentStatus Status,
        NodeFileManifestDto? File = null,
        string? Error = null);
}
