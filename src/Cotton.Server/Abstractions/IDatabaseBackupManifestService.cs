// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;

namespace Cotton.Server.Abstractions
{
    public interface IDatabaseBackupManifestService
    {
        Task<ResolvedBackupManifest?> TryGetLatestManifestAsync(CancellationToken cancellationToken = default);

        Task<BackupManifestPointer?> ReadPointerAsync(CancellationToken cancellationToken = default);

        Task<BackupManifest> ReadManifestAsync(BackupManifestReference backup, CancellationToken cancellationToken = default);

        Task<ResolvedBackupManifest> PublishAsync(BackupManifest manifest, CancellationToken cancellationToken = default);
    }
}
