// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Immutable event describing a failed database-integrity verification.
/// </summary>
public sealed record DatabaseIntegrityFailure(
    string EntityName,
    string EntityKey,
    string Boundary,
    DateTime DetectedAtUtc);
