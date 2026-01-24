
namespace Cotton.Storage.Abstractions
{
    public interface IStorageBackend
    {
        void CleanupTempFiles(TimeSpan ttl);
        Task<bool> DeleteAsync(string uid);
        Task<bool> ExistsAsync(string uid);
        Task<Stream> ReadAsync(string uid);
        Task WriteAsync(string uid, Stream stream);
    }
}
