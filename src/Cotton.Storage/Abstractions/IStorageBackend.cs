namespace Cotton.Storage.Abstractions
{
    public interface IStorageBackend
    {
        Task<bool> DeleteAsync(string uid);
        Task<Stream> ReadAsync(string uid);
        Task WriteAsync(string uid, Stream stream);
    }
}
