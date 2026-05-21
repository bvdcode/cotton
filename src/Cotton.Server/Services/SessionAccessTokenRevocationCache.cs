// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services
{
    public sealed class SessionAccessTokenRevocationCache : IDisposable
    {
        private const long EntrySize = 1;
        private const long MaxEntries = 1_000_000;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions
        {
            SizeLimit = MaxEntries,
        });

        public bool IsRevoked(Guid userId, string sessionId)
        {
            return _cache.TryGetValue(RevokedKey(userId, sessionId), out bool revoked) && revoked;
        }

        public bool TryGetActive(Guid userId, string sessionId, out bool active)
        {
            return _cache.TryGetValue(ActiveKey(userId, sessionId), out active);
        }

        public void MarkActive(Guid userId, string sessionId, TimeSpan duration)
        {
            _cache.Set(
                ActiveKey(userId, sessionId),
                true,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = duration,
                    Size = EntrySize,
                });
        }

        public void MarkRevoked(Guid userId, string sessionId, TimeSpan duration)
        {
            _cache.Set(
                RevokedKey(userId, sessionId),
                true,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = duration,
                    Size = EntrySize,
                });
            _cache.Remove(ActiveKey(userId, sessionId));
        }

        public void Dispose()
        {
            _cache.Dispose();
        }

        private static string ActiveKey(Guid userId, string sessionId)
            => $"auth-session-active:{userId:N}:{sessionId}";

        private static string RevokedKey(Guid userId, string sessionId)
            => $"auth-session-revoked:{userId:N}:{sessionId}";
    }
}
