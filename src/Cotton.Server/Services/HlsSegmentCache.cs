// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Mediana.Core.Timing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    public class HlsSegmentCache : IDisposable
    {
        private static readonly TimeSpan VideoPlanLifetime = TimeSpan.FromHours(1);
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
            if (_cache.TryGetValue<byte[]>(key, out byte[]? value) && value is { Length: > 0 })
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

        internal bool TryGetPlan(Guid manifestId, [NotNullWhen(true)] out HlsVideoPlan? plan) =>
            _cache.TryGetValue(manifestId, out plan) && plan is not null;

        internal void SetPlan(Guid manifestId, HlsVideoPlan plan)
        {
            _cache.Set(manifestId, plan, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = VideoPlanLifetime,
                Size = 256L + (long)plan.Segments.Count * Unsafe.SizeOf<MediaTimeRange>(),
            });
        }

        public void Dispose() => _cache.Dispose();
    }
}
