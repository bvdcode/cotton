namespace Cotton.Storage.Abstractions
{
    public interface IStorageBackend
    {
        Task<Stream> ReadAsync(string uid);
        Task WriteAsync(string uid, Stream stream);
    }
}
