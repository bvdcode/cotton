
namespace Cotton.Server.Models.Dto
{
    public class LayoutStatsDto
    {
        public long SizeBytes { get; init; }
        public Guid LayoutId { get; init; }
        public int NodeCount { get; init; }
        public int FileCount { get; init; }
    }
}
