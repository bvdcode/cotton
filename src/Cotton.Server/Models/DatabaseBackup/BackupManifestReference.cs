// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

namespace Cotton.Server.Models.DatabaseBackup
{
    public record BackupManifestReference(
        string ManifestStorageKey,
        string BackupId,
        DateTime CreatedAtUtc,
        long DumpSizeBytes,
        int ChunkCount,
        string DumpContentHash,
        string SourceDatabase,
        string SourceHost,
        string SourcePort)
    {
        public static BackupManifestReference FromManifest(string storageKey, BackupManifest manifest) =>
            new(storageKey, manifest.BackupId, manifest.CreatedAtUtc, manifest.DumpSizeBytes,
                manifest.ChunkCount, manifest.DumpContentHash, manifest.SourceDatabase,
                manifest.SourceHost, manifest.SourcePort);

        public LatestDatabaseBackupDto ToDto(DateTime pointerUpdatedAt) => new()
        {
            BackupId = BackupId,
            CreatedAtUtc = CreatedAtUtc,
            PointerUpdatedAtUtc = pointerUpdatedAt,
            DumpSizeBytes = DumpSizeBytes,
            ChunkCount = ChunkCount,
            DumpContentHash = DumpContentHash,
            SourceDatabase = SourceDatabase,
            SourceHost = SourceHost,
            SourcePort = SourcePort,
        };
    }
}
