namespace Cotton.Server.Abstractions
{
    public interface IStorage
    {
        Stream GetBlobStream(string[] uids);
        Task WriteFileAsync(string uid, Stream stream, CancellationToken ct = default);
    }
}