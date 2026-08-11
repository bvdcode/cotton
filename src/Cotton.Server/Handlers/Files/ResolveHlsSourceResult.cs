// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Represents an HLS source lookup result.
    /// </summary>
    public record ResolveHlsSourceResult(
        ResolveHlsSourceStatus Status,
        NodeFile? NodeFile = null,
        DownloadToken? DownloadToken = null);
}
