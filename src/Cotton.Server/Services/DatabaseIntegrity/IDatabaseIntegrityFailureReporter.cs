// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Reports rejected integrity checks to administrators or a startup-safe placeholder.
/// </summary>
public interface IDatabaseIntegrityFailureReporter
{
    /// <summary>Publishes one integrity failure event.</summary>
    void Report(DatabaseIntegrityFailure failure);
}
