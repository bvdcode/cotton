// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    public class DatabaseIntegrityException(
        string entityName,
        string entityKey)
        : Exception($"Database integrity verification failed for {entityName} '{entityKey}'.")
    {
        public string EntityName { get; } = entityName;

        public string EntityKey { get; } = entityKey;
    }
}
