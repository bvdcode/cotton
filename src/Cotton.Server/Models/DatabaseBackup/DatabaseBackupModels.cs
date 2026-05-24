// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.DatabaseBackup
{
    /// <summary>
    /// Describes backup chunk information.
    /// </summary>
    public sealed record BackupChunkInfo(int Order, string StorageKey, int SizeBytes);

    /// <summary>
    /// Describes a backup manifest.
    /// </summary>
    public sealed record BackupManifest(
        int SchemaVersion,
        string BackupId,
        DateTime CreatedAtUtc,
        string Contains,
        string DumpFormat,
        string SourceDatabase,
        string SourceHost,
        string SourcePort,
        string HashAlgorithm,
        int ChunkSizeBytes,
        long DumpSizeBytes,
        string DumpContentHash,
        int ChunkCount,
        TimeSpan Elapsed,
        IReadOnlyList<BackupChunkInfo> Chunks);

    /// <summary>
    /// Points to the active backup manifest.
    /// </summary>
    public sealed record BackupManifestPointer(
        int SchemaVersion,
        string LogicalKey,
        DateTime UpdatedAtUtc,
        string LatestManifestStorageKey,
        string LatestBackupId);

    /// <summary>
    /// Describes a resolved backup manifest.
    /// </summary>
    public sealed record ResolvedBackupManifest(
        string ManifestStorageKey,
        BackupManifestPointer Pointer,
        BackupManifest Manifest);
}
