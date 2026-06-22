// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Caches session access token revocation state.
    /// </summary>
    public class SessionAccessTokenRevocationCache : IDisposable
    {
        private const long EntrySize = 1;
        private const long MaxEntries = 1_000_000;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions
        {
            SizeLimit = MaxEntries,
        });

        /// <summary>
        /// Indicates whether revoked.
        /// </summary>
        public bool IsRevoked(Guid userId, string sessionId)
        {
            return _cache.TryGetValue(RevokedKey(userId, sessionId), out bool revoked) && revoked;
        }

        /// <summary>
        /// Attempts to get active.
        /// </summary>
        public bool TryGetActive(Guid userId, string sessionId, out bool active)
        {
            return _cache.TryGetValue(ActiveKey(userId, sessionId), out active);
        }

        /// <summary>
        /// Executes mark active.
        /// </summary>
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

        /// <summary>
        /// Executes mark revoked.
        /// </summary>
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

        /// <summary>
        /// Releases resources held by this instance.
        /// </summary>
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
