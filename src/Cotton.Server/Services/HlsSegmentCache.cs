// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    public sealed class HlsSegmentCacheOptions
    {
        public long SizeLimitBytes { get; set; } = 512L * 1024 * 1024;
    }

    public sealed class HlsSegmentCache : IDisposable
    {
        private readonly MemoryCache _cache;
        private readonly long _sizeLimit;

        public HlsSegmentCache(IOptions<HlsSegmentCacheOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _sizeLimit = Math.Max(0, options.Value.SizeLimitBytes);
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = _sizeLimit,
            });
        }

        public long SizeLimitBytes => _sizeLimit;

        public static string BuildKey(Guid fileManifestId, string quality, int segmentIndex) =>
            $"{fileManifestId:N}:{quality}:{segmentIndex}";

        public bool TryGet(string key, [NotNullWhen(true)] out byte[]? bytes)
        {
            if (_cache.TryGetValue<byte[]>(key, out var value) && value is { Length: > 0 })
            {
                bytes = value;
                return true;
            }

            bytes = null;
            return false;
        }

        public void Set(string key, byte[] bytes)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            ArgumentNullException.ThrowIfNull(bytes);

            if (bytes.Length == 0 || (_sizeLimit > 0 && bytes.Length > _sizeLimit))
            {
                return;
            }

            _cache.Set(key, bytes, new MemoryCacheEntryOptions
            {
                Size = bytes.Length,
            });
        }

        public void Dispose() => _cache.Dispose();
    }
}
