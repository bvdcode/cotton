// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public record RefreshTokenRevocationResult(
        int RevokedTokens,
        IReadOnlyList<string> SessionIds);
}
