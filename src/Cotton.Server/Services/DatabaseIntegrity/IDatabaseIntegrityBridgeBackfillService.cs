// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Performs the one-release bridge that signs pre-existing rows during the first integrity rollout.
/// </summary>
public interface IDatabaseIntegrityBridgeBackfillService
{
    /// <summary>Signs unsigned phase-one rows only when the database is still in a clean pre-integrity state.</summary>
    Task<int> BackfillUnsignedPhaseOneRowsAsync(CancellationToken cancellationToken);
}
