// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Database integrity protection diagnostics.
    /// </summary>
    public class DatabaseIntegrityDiagnosticsDto
    {
        /// <summary>
        /// Whether database integrity protection is enabled.
        /// </summary>
        public bool Enabled { get; init; }

        /// <summary>
        /// Number of entity types covered by integrity protection.
        /// </summary>
        public int ProtectedEntityTypes { get; init; }

        /// <summary>
        /// Number of protected rows that are missing an integrity signature.
        /// </summary>
        public int UnsignedProtectedRows { get; init; }
    }
}
