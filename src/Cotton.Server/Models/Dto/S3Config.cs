namespace Cotton.Server.Models.Dto
{
    public class S3Config
    {
        public string AccessKey { get; init; } = null!;
        public string SecretKey { get; init; } = null!;
        public string Endpoint { get; init; } = null!;
        public string Region { get; init; } = null!;
        public string Bucket { get; init; } = null!;
    }
}
