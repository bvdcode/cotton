// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Defines the database backup manifest service contract used by the server runtime.
    /// </summary>
    public interface IDatabaseBackupManifestService
    {
        /// <summary>
        /// Attempts to get latest manifest.
        /// </summary>
        Task<ResolvedBackupManifest?> TryGetLatestManifestAsync(CancellationToken cancellationToken = default);
    }
}
