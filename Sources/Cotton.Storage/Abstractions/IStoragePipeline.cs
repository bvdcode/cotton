namespace Cotton.Storage.Abstractions
{
    public interface IStoragePipeline
    {
        Task<Stream> ReadAsync(string uid);
        Task WriteAsync(string uid, Stream stream);
    }
}
