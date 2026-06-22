// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Immutable event describing a failed database-integrity verification.
/// </summary>
/// <param name="EntityName">Stable descriptor name for the protected table.</param>
/// <param name="EntityKey">Stable row key written into the canonical payload.</param>
/// <param name="Boundary">Human-readable read boundary where verification failed.</param>
/// <param name="DetectedAtUtc">UTC timestamp when the failure was detected.</param>
public sealed record DatabaseIntegrityFailure(
    string EntityName,
    string EntityKey,
    string Boundary,
    DateTime DetectedAtUtc);
