namespace Cotton.Server.Models.Dto
{
    public class StorageUsageStatsDto
    {
        public string StorageType { get; init; } = string.Empty;

        public long TotalUniqueChunkCount { get; init; }
        public long TotalUniqueChunkPlainSizeBytes { get; init; }

        public long ReferencedUniqueChunkCount { get; init; }
        public long ReferencedUniqueChunkPlainSizeBytes { get; init; }

        public long ReferencedLogicalChunkCount { get; init; }
        public long ReferencedLogicalPlainSizeBytes { get; init; }

        public long DeduplicatedUniqueChunkCount { get; init; }
        public long DedupSavedBytes { get; init; }

        public long PendingGcChunkCount { get; init; }
        public long PendingGcSizeBytes { get; init; }

        public long OverdueGcChunkCount { get; init; }
        public long OverdueGcSizeBytes { get; init; }

        public bool PhysicalStorageScanCompleted { get; init; }
        public long? PhysicalStoredObjectCount { get; init; }
        public long? PhysicalStoredSizeBytes { get; init; }
        public long? CompressionGainBytes { get; init; }
        public long? PhysicalStorageScanDurationMs { get; init; }
        public int PhysicalStorageScanErrors { get; init; }
    }
}
