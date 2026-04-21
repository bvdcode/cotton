namespace Cotton.Server.Models.Dto
{
    public class GcChunkTimelineBucketDto
    {
        public DateTime BucketStartUtc { get; init; }
        public long ChunkCount { get; init; }
        public long SizeBytes { get; init; }
    }
}
