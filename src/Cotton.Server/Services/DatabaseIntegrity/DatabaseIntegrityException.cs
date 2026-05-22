// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Exception thrown when a protected database row fails integrity verification.
/// </summary>
public sealed class DatabaseIntegrityException : Exception
{
    /// <summary>Initializes a new exception for the rejected protected row.</summary>
    public DatabaseIntegrityException(string entityName, string entityKey)
        : base($"Database integrity verification failed for {entityName} '{entityKey}'.")
    {
        EntityName = entityName;
        EntityKey = entityKey;
    }

    /// <summary>Gets the protected entity group that failed verification.</summary>
    public string EntityName { get; }
    /// <summary>Gets the row diagnostics key that failed verification.</summary>
    public string EntityKey { get; }
}
