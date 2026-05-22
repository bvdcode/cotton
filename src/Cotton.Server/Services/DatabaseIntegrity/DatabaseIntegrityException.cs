// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

public sealed class DatabaseIntegrityException : Exception
{
    public DatabaseIntegrityException(string entityName, string entityKey)
        : base($"Database integrity verification failed for {entityName} '{entityKey}'.")
    {
        EntityName = entityName;
        EntityKey = entityKey;
    }

    public string EntityName { get; }
    public string EntityKey { get; }
}
