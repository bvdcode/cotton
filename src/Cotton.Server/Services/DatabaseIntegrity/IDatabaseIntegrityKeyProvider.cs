// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Provides disposable HMAC instances keyed for database row integrity signatures.
/// </summary>
public interface IDatabaseIntegrityKeyProvider
{
    /// <summary>Creates a new HMAC instance. Callers own the returned disposable object.</summary>
    HMACSHA256 CreateHmac();
}
