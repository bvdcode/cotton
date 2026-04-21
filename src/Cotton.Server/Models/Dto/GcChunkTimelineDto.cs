namespace Cotton.Server.Models.Dto
{
    public class GcChunkTimelineDto
    {
        public string Bucket { get; init; } = "hour";
        public int TimezoneOffsetMinutes { get; init; }
        public DateTime FromUtc { get; init; }
        public DateTime ToUtc { get; init; }
        public DateTime GeneratedAtUtc { get; init; }
        public long TotalChunks { get; init; }
        public long TotalSizeBytes { get; init; }
        public IReadOnlyList<GcChunkTimelineBucketDto> Buckets { get; init; } = [];
        public StorageUsageStatsDto Storage { get; init; } = new();
    }
}
