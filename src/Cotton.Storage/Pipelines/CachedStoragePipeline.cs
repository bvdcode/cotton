using Cotton.Storage.Abstractions;
using System.Runtime.Caching;

namespace Cotton.Storage.Pipelines
{
    public class CachedStoragePipeline : IStoragePipeline
    {
        private const int MaxCacheSizeBytes = 100 * 1024 * 1024;
        private const int MaxItemSizeBytes = 1 * 1024 * 1024;
        private static readonly MemoryCache _cache = new("CachedStoragePipeline");

        public Task<Stream> ReadAsync(string uid)
        {
            throw new NotImplementedException();
        }

        public Task WriteAsync(string uid, Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
