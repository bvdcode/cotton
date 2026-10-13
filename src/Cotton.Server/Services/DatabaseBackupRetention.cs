// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;

namespace Cotton.Server.Services
{
    public static class DatabaseBackupRetention
    {
        public const int MinimumCount = 3;
        public static readonly TimeSpan MinimumAge = TimeSpan.FromDays(7);

        public static BackupManifestReference[] Retain(
            IEnumerable<BackupManifestReference> backups, DateTime now) =>
            backups.DistinctBy(backup => backup.ManifestStorageKey)
                .OrderByDescending(backup => backup.CreatedAtUtc)
                .Where((backup, index) => index < MinimumCount || backup.CreatedAtUtc >= now - MinimumAge)
                .ToArray();
    }
}
