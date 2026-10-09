// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class StaticBackupManifestService(ResolvedBackupManifest? _latestBackup) : IDatabaseBackupManifestService
    {
        public Task<ResolvedBackupManifest?> TryGetLatestManifestAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_latestBackup);
        }

        public Task<BackupManifestPointer?> ReadPointerAsync(CancellationToken cancellationToken = default)
        {
            if (_latestBackup is null)
            {
                return Task.FromResult<BackupManifestPointer?>(null);
            }
            return Task.FromResult<BackupManifestPointer?>(_latestBackup.Pointer with
            {
                History = [BackupManifestReference.FromManifest(_latestBackup.ManifestStorageKey, _latestBackup.Manifest)]
            });
        }

        public Task<BackupManifest> ReadManifestAsync(BackupManifestReference backup, CancellationToken cancellationToken = default) =>
            Task.FromResult(_latestBackup!.Manifest);

        public Task<ResolvedBackupManifest> PublishAsync(BackupManifest manifest, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
