// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    [Obsolete("OBSOLETE TRANSITION: remove this exception and the unsigned-row branch after the 0.5 cutover window.")]
    public class DatabaseIntegritySignatureMissingException(
        string entityName,
        string entityKey)
        : Exception(
            $"Database integrity signature is missing for {entityName} '{entityKey}'. "
            + "Start Cotton 0.4.35 with the same database, storage, and master key, "
            + "wait for the database-integrity transition to complete, then upgrade again.")
    {
        public string EntityName { get; } = entityName;

        public string EntityKey { get; } = entityKey;
    }
}
