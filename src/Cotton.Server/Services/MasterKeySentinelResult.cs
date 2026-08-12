// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public record MasterKeySentinelResult(bool Success, bool Created, string? Error)
    {
        public static MasterKeySentinelResult Ok(bool created) =>
            new(true, created, null);

        public static MasterKeySentinelResult Fail(string error) => new(false, false, error);
    }
}
