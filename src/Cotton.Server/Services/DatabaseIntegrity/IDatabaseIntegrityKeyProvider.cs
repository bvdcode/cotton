// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Creates HMAC instances keyed by a subkey derived from the Cotton master key.
    /// </summary>
    public interface IDatabaseIntegrityKeyProvider
    {
        /// <summary>Creates a new disposable HMAC instance for one signing or verification operation.</summary>
        HMACSHA256 CreateHmac();
    }
}
