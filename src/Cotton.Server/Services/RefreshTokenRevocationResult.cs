// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Describes refresh-token revocation work completed by a revocation service call.
    /// </summary>
    public record RefreshTokenRevocationResult(
        int RevokedTokens,
        IReadOnlyList<string> SessionIds);
}
