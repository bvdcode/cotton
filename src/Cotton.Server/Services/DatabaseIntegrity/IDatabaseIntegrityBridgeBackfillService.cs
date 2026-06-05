// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Performs startup bridge signing for protected rows.
/// </summary>
public interface IDatabaseIntegrityBridgeBackfillService
{
    /// <summary>Re-signs protected rows from the current database state.</summary>
    Task<int> BackfillUnsignedPhaseOneRowsAsync(CancellationToken cancellationToken);
}
