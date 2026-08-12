// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;

namespace Cotton.Server.Abstractions
{
    public interface IDatabaseBackupManifestService
    {
        Task<ResolvedBackupManifest?> TryGetLatestManifestAsync(CancellationToken cancellationToken = default);
    }
}
