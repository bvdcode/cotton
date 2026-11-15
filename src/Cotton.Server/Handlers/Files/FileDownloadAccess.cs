// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Handlers.Files
{
    public record FileDownloadAccess(Guid UserId, DownloadToken? ShareToken = null);
}
