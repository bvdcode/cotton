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
        /// <summary>
        /// Gets the magic marker written at the beginning of every canonical payload.
        /// </summary>
        public const string PayloadMagic = "Cotton.DbIntegrity.Row";

        /// <summary>
        /// Gets the payload envelope format version.
        /// </summary>
        public const int PayloadFormatVersion = 1;

        /// <summary>
        /// Gets the canonical field writer version.
        /// </summary>
        public const int CanonicalWriterVersion = 1;

        /// <summary>
        /// Gets the MAC algorithm used by the integrity protector.
        /// </summary>
        public const string MacAlgorithm = "HMAC-SHA-256";
    }
}
