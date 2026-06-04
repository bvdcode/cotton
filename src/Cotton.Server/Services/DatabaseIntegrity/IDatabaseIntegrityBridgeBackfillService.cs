// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Performs startup bridge signing for pre-existing rows and supported descriptor upgrades.
/// </summary>
public interface IDatabaseIntegrityBridgeBackfillService
{
    /// <summary>Signs unsigned rows and upgrades rows whose legacy MAC validates against a known descriptor.</summary>
    Task<int> BackfillUnsignedPhaseOneRowsAsync(CancellationToken cancellationToken);
}
