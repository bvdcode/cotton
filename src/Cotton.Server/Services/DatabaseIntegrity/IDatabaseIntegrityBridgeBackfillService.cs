// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

public interface IDatabaseIntegrityBridgeBackfillService
{
    Task<int> BackfillUnsignedPhaseOneRowsAsync(CancellationToken cancellationToken);
}
