// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services
{
    public class SharedUploadSessionCache(IMemoryCache cache)
    {
        public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
        private readonly object _gate = new();

        public void Record(Guid shareId, Guid nodeId, Guid sessionId, string hash)
        {
            lock (_gate)
            {
                HashSet<string> hashes = cache.GetOrCreate((nameof(SharedUploadSessionCache), shareId, nodeId, sessionId), entry =>
                {
                    entry.SlidingExpiration = IdleTimeout;
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                })!;
                hashes.Add(hash);
            }
        }

        public bool ContainsAll(Guid shareId, Guid nodeId, Guid sessionId, IEnumerable<string> hashes)
        {
            lock (_gate)
            {
                return cache.TryGetValue((nameof(SharedUploadSessionCache), shareId, nodeId, sessionId), out HashSet<string>? uploaded)
                    && uploaded is not null && hashes.All(uploaded.Contains);
            }
        }
    }
}
