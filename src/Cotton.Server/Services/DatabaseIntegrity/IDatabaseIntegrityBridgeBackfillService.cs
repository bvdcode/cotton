// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Signs existing protected rows during the first deployment that introduces integrity metadata.
/// </summary>
public interface IDatabaseIntegrityBridgeBackfillService
{
    /// <summary>Backfills rows that predate integrity metadata and returns the number of signed rows.</summary>
    Task<int> BackfillUnsignedPhaseOneRowsAsync(CancellationToken cancellationToken);
}
