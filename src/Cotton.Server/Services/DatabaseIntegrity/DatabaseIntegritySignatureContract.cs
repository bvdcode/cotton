// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Defines the current database row integrity signature contract.
    /// </summary>
    /// <remarks>
    /// Changing any value here invalidates stored MACs and requires the transition re-sign job to run.
    /// </remarks>
    public static class DatabaseIntegritySignatureContract
    {
        public const string PayloadMagic = "Cotton.DbIntegrity.Row";

        public const int PayloadFormatVersion = 1;

        public const int CanonicalWriterVersion = 1;

        public const string MacAlgorithm = "HMAC-SHA-256";
    }
}
