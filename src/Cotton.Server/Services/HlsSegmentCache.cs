// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Caches hls segment state.
    /// </summary>
    public class HlsSegmentCache : IDisposable
    {
        private readonly MemoryCache _cache;
        private readonly long _sizeLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="HlsSegmentCache"/> type.
        /// </summary>
        public HlsSegmentCache(IOptions<HlsSegmentCacheOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _sizeLimit = Math.Max(0, options.Value.SizeLimitBytes);
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = _sizeLimit,
            });
        }

        /// <summary>
        /// Gets the size limit bytes.
        /// </summary>
        public long SizeLimitBytes => _sizeLimit;

        /// <summary>
        /// Builds key.
        /// </summary>
        public static string BuildKey(Guid fileManifestId, string quality, int segmentIndex) =>
            $"{fileManifestId:N}:{quality}:{segmentIndex}";

        /// <summary>
        /// Attempts to get value.
        /// </summary>
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

        /// <summary>
        /// Sets value.
        /// </summary>
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

        /// <summary>
        /// Releases resources held by this instance.
        /// </summary>
        public void Dispose() => _cache.Dispose();
    }
}
