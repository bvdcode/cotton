// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services
{
    internal class ArchiveCrcCache : IDisposable
    {
        private const int MaximumEntries = 100_000;
        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(1);
        private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaximumEntries });
        private readonly Lock _lock = new();

        public ArchiveCrcState Get(string? archiveIdentity, long entryOffset)
        {
            if (archiveIdentity is null)
            {
                return new();
            }
            lock (_lock)
            {
                return _cache.GetOrCreate((archiveIdentity, entryOffset), entry =>
                {
                    entry.SetSize(1).SetSlidingExpiration(Lifetime);
                    return new ArchiveCrcState();
                })!;
            }
        }

        public void Dispose() => _cache.Dispose();
    }
}
