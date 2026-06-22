// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.DatabaseBackup
{
    /// <summary>
    /// Points to the active backup manifest.
    /// </summary>
    public record BackupManifestPointer(
        int SchemaVersion,
        string LogicalKey,
        DateTime UpdatedAtUtc,
        string LatestManifestStorageKey,
        string LatestBackupId);
}
