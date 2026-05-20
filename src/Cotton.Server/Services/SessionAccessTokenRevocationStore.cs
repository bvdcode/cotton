// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services
{
    public sealed class SessionAccessTokenRevocationStore(
        CottonDbContext _dbContext,
        IMemoryCache _cache)
    {
        private static readonly TimeSpan ActiveSessionCacheDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RevokedSessionCacheDuration = TimeSpan.FromMinutes(65);
        private static readonly TimeSpan AccessTokenClockSkew = TimeSpan.FromMinutes(5);

        public void Revoke(Guid userId, string sessionId, TimeSpan accessTokenLifetime)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            TimeSpan ttl = accessTokenLifetime > TimeSpan.Zero
                ? accessTokenLifetime + AccessTokenClockSkew
                : RevokedSessionCacheDuration;

            _cache.Set(RevokedKey(userId, sessionId), true, ttl);
            _cache.Remove(ActiveKey(userId, sessionId));
        }

        public async Task<bool> IsRevokedAsync(
            Guid userId,
            string? sessionId,
            CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty || string.IsNullOrWhiteSpace(sessionId))
            {
                return true;
            }

            if (_cache.TryGetValue(RevokedKey(userId, sessionId), out bool revoked) && revoked)
            {
                return true;
            }

            if (_cache.TryGetValue(ActiveKey(userId, sessionId), out bool active))
            {
                return !active;
            }

            bool hasActiveRefreshToken = await _dbContext.RefreshTokens
                .AsNoTracking()
                .AnyAsync(
                    x => x.UserId == userId
                        && x.SessionId == sessionId
                        && x.RevokedAt == null,
                    cancellationToken);

            if (hasActiveRefreshToken)
            {
                _cache.Set(ActiveKey(userId, sessionId), true, ActiveSessionCacheDuration);
                return false;
            }

            _cache.Set(RevokedKey(userId, sessionId), true, RevokedSessionCacheDuration);
            return true;
        }

        private static string ActiveKey(Guid userId, string sessionId)
            => $"auth-session-active:{userId:N}:{sessionId}";

        private static string RevokedKey(Guid userId, string sessionId)
            => $"auth-session-revoked:{userId:N}:{sessionId}";
    }
}
