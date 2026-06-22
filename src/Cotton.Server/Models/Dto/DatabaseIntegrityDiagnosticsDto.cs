// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the database integrity diagnostics API payload.
    /// </summary>
    public class DatabaseIntegrityDiagnosticsDto
    {
        /// <summary>
        /// Gets a value indicating whether database integrity protection is enabled.
        /// </summary>
        public bool Enabled { get; init; }
        /// <summary>
        /// Gets or sets protected entity types.
        /// </summary>
        public int ProtectedEntityTypes { get; init; }
        /// <summary>
        /// Gets or sets unsigned protected rows.
        /// </summary>
        public int UnsignedProtectedRows { get; init; }
    }
}
