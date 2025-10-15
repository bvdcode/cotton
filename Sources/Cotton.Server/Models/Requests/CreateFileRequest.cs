namespace Cotton.Server.Models.Requests
{
    public class CreateFileRequest
    {
        public Guid UserLayoutNodeId { get; set; }
        public string[] ChunkHashes { get; set; } = [];
        public string Name { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public string Sha256 { get; set; } = null!;
    }
}
