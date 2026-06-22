// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Crypto;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents the result of master key sentinel.
    /// </summary>
    public record MasterKeySentinelResult(bool Success, bool Created, bool Repaired, string? Error)
    {
        /// <summary>
        /// Creates a successful compatibility probe result.
        /// </summary>
        public static MasterKeySentinelResult Ok(bool created, bool repaired = false) =>
            new(true, created, repaired, null);

        /// <summary>
        /// Executes fail.
        /// </summary>
        public static MasterKeySentinelResult Fail(string error) => new(false, false, false, error);
    }
}
