// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Exception thrown when a protected database row does not match its integrity metadata.
    /// </summary>
    public class DatabaseIntegrityException : Exception
    {
        /// <summary>
        /// Initializes a failure for the protected entity and row key that failed verification.
        /// </summary>
        public DatabaseIntegrityException(string entityName, string entityKey)
            : base($"Database integrity verification failed for {entityName} '{entityKey}'.")
        {
            EntityName = entityName;
            EntityKey = entityKey;
        }

        /// <summary>
        /// Gets the stable descriptor name for the protected table.
        /// </summary>
        public string EntityName { get; }

        /// <summary>
        /// Gets the stable row key that failed verification.
        /// </summary>
        public string EntityKey { get; }
    }
}
