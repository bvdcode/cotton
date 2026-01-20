using Cotton.Storage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Cotton.Storage.Pipelines
{
    public class CachedStoragePipeline(IServiceProvider _serviceProvider) : IStoragePipeline
    {
        private const int MaxCacheSizeBytes = 100 * 1024 * 1024;
        private const int MaxItemSizeBytes = 100 * 1024;
        private static readonly ConcurrentDictionary<string, byte[]> _cache = new();
        private readonly FileStoragePipeline _innerStorage = ActivatorUtilities.CreateInstance<FileStoragePipeline>(_serviceProvider);
        private readonly ILogger<CachedStoragePipeline> _logger = _serviceProvider.GetRequiredService<ILogger<CachedStoragePipeline>>();

        public Task<bool> ExistsAsync(string uid)
        {
            return _innerStorage.ExistsAsync(uid);
        }

        public Task<bool> DeleteAsync(string uid)
        {
            return _innerStorage.DeleteAsync(uid);
        }

        public async Task<Stream> ReadAsync(string uid, PipelineContext? context = null)
        {
            var cached = _cache.TryGetValue(uid, out var data);
            if (cached)
            {
                _logger.LogDebug("Cache hit for UID {UID}, cache size: {CacheSize} bytes, objects: {Count}",
                    uid, _cache.Sum(kvp => kvp.Value.Length), _cache.Count);
                return new MemoryStream(data!, writable: false);
            }
            var stream = await _innerStorage.ReadAsync(uid, context);
            if (context == null)
            {
                _logger.LogDebug("Item has no context, bypassing cache for UID {UID}", uid);
                return stream;
            }
            if (!context.StoreInMemoryCache && context.FileSizeBytes.HasValue && context.FileSizeBytes.Value > MaxItemSizeBytes)
            {
                _logger.LogDebug("Item size {ItemSize} bytes exceeds max cacheable size for UID {UID}, bypassing cache",
                    context.FileSizeBytes.Value, uid);
                return stream;
            }
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            data = memoryStream.ToArray();
            _cache[uid] = data;
            _logger.LogDebug("Cache miss for UID {UID}, cached item size: {ItemSize} bytes, total cache size: {CacheSize} bytes, objects: {Count}",
                uid, data.Length, _cache.Sum(kvp => kvp.Value.Length), _cache.Count);
            // Evict items if cache size exceeds limit
            while (_cache.Sum(kvp => kvp.Value.Length) > MaxCacheSizeBytes)
            {
                var firstKey = _cache.Keys.FirstOrDefault();
                if (firstKey != null)
                {
                    _cache.TryRemove(firstKey, out _);
                    _logger.LogDebug("Evicted UID {UID} from cache to maintain cache size", firstKey);
                }
            }
            return new MemoryStream(data, writable: false);
        }

        public Task WriteAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            // Bypass cache for writes
            return _innerStorage.WriteAsync(uid, stream, context);
        }
    }
}
