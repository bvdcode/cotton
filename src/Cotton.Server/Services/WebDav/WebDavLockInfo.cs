// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.WebDav
{
    public record WebDavLockInfo(
        Guid UserId,
        string Path,
        string Token,
        DateTimeOffset ExpiresAt);
}
