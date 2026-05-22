// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Reports database-integrity failures after a security-sensitive read rejects a row.
/// </summary>
public interface IDatabaseIntegrityFailureReporter
{
    /// <summary>Queues or records a detected integrity failure for administrator visibility.</summary>
    void Report(DatabaseIntegrityFailure failure);
}
