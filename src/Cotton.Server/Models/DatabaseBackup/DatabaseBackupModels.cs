namespace Cotton.Server.Models.DatabaseBackup
{
    public sealed record BackupChunkInfo(int Order, string StorageKey, int SizeBytes);

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

    public sealed record BackupManifestPointer(
        int SchemaVersion,
        string LogicalKey,
        DateTime UpdatedAtUtc,
        string LatestManifestStorageKey,
        string LatestBackupId);

    public sealed record ResolvedBackupManifest(
        string ManifestStorageKey,
        BackupManifestPointer Pointer,
        BackupManifest Manifest);
}
