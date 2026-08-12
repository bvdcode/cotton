// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// No-op integrity failure reporter used when notifications are intentionally unavailable.
    /// </summary>
    public class NullDatabaseIntegrityFailureReporter : IDatabaseIntegrityFailureReporter
    {
        public static readonly NullDatabaseIntegrityFailureReporter Instance = new();

        private NullDatabaseIntegrityFailureReporter()
        {
        }

        public void Report(DatabaseIntegrityFailure failure)
        {
        }
    }
}
