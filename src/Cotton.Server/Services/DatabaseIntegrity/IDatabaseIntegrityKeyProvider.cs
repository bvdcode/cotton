// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Creates HMAC instances keyed by the current database-integrity signing key.
/// </summary>
public interface IDatabaseIntegrityKeyProvider
{
    /// <summary>Creates a new disposable HMAC instance for one signing operation.</summary>
    HMACSHA256 CreateHmac();
}

/// <summary>
/// Creates HMAC instances for every key that is still allowed to verify stored database integrity MACs.
/// </summary>
public interface IDatabaseIntegrityVerificationKeyProvider
{
    /// <summary>Creates disposable HMAC instances for one verification operation.</summary>
    IReadOnlyList<HMACSHA256> CreateVerificationHmacs();
}
