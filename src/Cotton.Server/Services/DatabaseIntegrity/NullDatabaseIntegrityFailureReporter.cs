// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Startup-safe failure reporter used before notification services are available.
/// </summary>
public sealed class NullDatabaseIntegrityFailureReporter : IDatabaseIntegrityFailureReporter
{
    /// <summary>Shared no-op reporter instance.</summary>
    public static readonly NullDatabaseIntegrityFailureReporter Instance = new();

    private NullDatabaseIntegrityFailureReporter()
    {
    }

    /// <inheritdoc />
    public void Report(DatabaseIntegrityFailure failure)
    {
    }
}
