namespace Cotton.Server.Models.Dto
{
    public class StorageUsageStatsDto
    {
        public string StorageType { get; init; } = string.Empty;

        public long TotalUniqueChunkCount { get; init; }
        public long TotalUniqueChunkPlainSizeBytes { get; init; }
        public long TotalUniqueChunkStoredSizeBytes { get; init; }

        public long ReferencedUniqueChunkCount { get; init; }
        public long ReferencedUniqueChunkPlainSizeBytes { get; init; }
        public long ReferencedUniqueChunkStoredSizeBytes { get; init; }

        public long ReferencedLogicalChunkCount { get; init; }
        public long ReferencedLogicalPlainSizeBytes { get; init; }

        public long DeduplicatedUniqueChunkCount { get; init; }
        public long DedupSavedBytes { get; init; }
        public long CompressionSavedBytes { get; init; }

        public long PendingGcChunkCount { get; init; }
        public long PendingGcStoredSizeBytes { get; init; }

        public long OverdueGcChunkCount { get; init; }
        public long OverdueGcStoredSizeBytes { get; init; }
    }
}
