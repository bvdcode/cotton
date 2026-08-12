// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;

namespace Cotton.Server.Handlers.Layouts
{
    public record CreateSharedArchiveDownloadLinkResult(
        CreateSharedArchiveDownloadLinkStatus Status,
        CreateArchiveDownloadLinkResult? Archive = null);
}
