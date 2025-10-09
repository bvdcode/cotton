namespace Cotton.Server.Abstractions
{
    public interface IStorage
    {
        Task WriteChunkAsync(string hash, Stream stream, CancellationToken ct = default);
    }
}