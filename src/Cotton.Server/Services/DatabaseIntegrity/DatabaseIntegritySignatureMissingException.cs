// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Indicates that a protected database row predates the required integrity-signature transition.
    /// </summary>
    [Obsolete("OBSOLETE TRANSITION: remove this exception and the unsigned-row branch after the 0.5 cutover window.")]
    public class DatabaseIntegritySignatureMissingException : Exception
    {
        /// <summary>
        /// Initializes the unsigned-row upgrade error.
        /// </summary>
        public DatabaseIntegritySignatureMissingException(string entityName, string entityKey)
            : base(
                $"Database integrity signature is missing for {entityName} '{entityKey}'. "
                + "Start Cotton 0.4.35 with the same database, storage, and master key, "
                + "wait for the database-integrity transition to complete, then upgrade again.")
        {
            EntityName = entityName;
            EntityKey = entityKey;
        }

        /// <summary>
        /// Gets the stable descriptor name for the unsigned protected row.
        /// </summary>
        public string EntityName { get; }

        /// <summary>
        /// Gets the stable key for the unsigned protected row.
        /// </summary>
        public string EntityKey { get; }
    }
}
