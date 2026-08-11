// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents the result of master key sentinel.
    /// </summary>
    public record MasterKeySentinelResult(bool Success, bool Created, string? Error)
    {
        /// <summary>
        /// Creates a successful sentinel result.
        /// </summary>
        public static MasterKeySentinelResult Ok(bool created) =>
            new(true, created, null);

        /// <summary>
        /// Executes fail.
        /// </summary>
        public static MasterKeySentinelResult Fail(string error) => new(false, false, error);
    }
}
