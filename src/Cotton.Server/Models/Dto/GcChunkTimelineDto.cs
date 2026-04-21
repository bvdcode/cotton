namespace Cotton.Server.Models.Dto
{
    public class GcChunkTimelineDto
    {
        public string Bucket { get; init; } = "hour";
        public int TimezoneOffsetMinutes { get; init; }
        public DateTime From { get; init; }
        public DateTime To { get; init; }
        public DateTime GeneratedAt { get; init; }
        public long TotalChunks { get; init; }
        public long TotalSizeBytes { get; init; }
        public IReadOnlyList<GcChunkTimelineBucketDto> Buckets { get; init; } = [];
        public StorageUsageStatsDto Storage { get; init; } = new();
    }
}
