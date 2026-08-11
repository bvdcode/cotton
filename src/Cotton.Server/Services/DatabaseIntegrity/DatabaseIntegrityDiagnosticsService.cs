// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Dto;

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Builds the security check-up snapshot for database integrity coverage.
    /// </summary>
    /// <remarks>
    /// The diagnostics path intentionally counts metadata state instead of recomputing every row MAC. Large folders and
    /// administrative screens can contain tens of thousands of rows; integrity verification stays on security-sensitive
    /// read boundaries where the application is about to trust a protected row.
    /// </remarks>
    public class DatabaseIntegrityDiagnosticsService(
        CottonDbContext _dbContext,
        IDatabaseIntegrityDescriptorRegistry _descriptors)
    {
        /// <summary>
        /// Returns counts of protected rows, missing metadata, and unsupported integrity versions.
        /// </summary>
        public async Task<DatabaseIntegrityDiagnosticsDto> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            int unsignedRows = 0;
            foreach (IDatabaseIntegrityDescriptor descriptor in _descriptors.All)
            {
                unsignedRows += await descriptor.CountInvalidMetadataRowsAsync(
                    _dbContext,
                    cancellationToken);
            }

            return new DatabaseIntegrityDiagnosticsDto
            {
                Enabled = true,
                ProtectedEntityTypes = _descriptors.All.Count,
                UnsignedProtectedRows = unsignedRows,
            };
        }
    }
}
