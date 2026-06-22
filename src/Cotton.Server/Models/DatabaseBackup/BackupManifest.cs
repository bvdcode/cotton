// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.DatabaseBackup
{
    /// <summary>
    /// Describes a backup manifest.
    /// </summary>
    public record BackupManifest(
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
}
