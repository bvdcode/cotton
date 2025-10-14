namespace Cotton.Server.Abstractions
{
    public interface IStorage
    {
        Stream GetBlobStream(string[] hashes);
        Task WriteChunkAsync(string hash, Stream stream, CancellationToken ct = default);
    }
}