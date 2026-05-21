// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    public sealed class SessionAccessTokenRevocationStore(
        CottonDbContext _dbContext,
        SessionAccessTokenRevocationCache _cache)
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

            _cache.MarkRevoked(userId, sessionId, ttl);
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

            if (_cache.IsRevoked(userId, sessionId))
            {
                return true;
            }

            if (_cache.TryGetActive(userId, sessionId, out bool active))
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
                _cache.MarkActive(userId, sessionId, ActiveSessionCacheDuration);
                return false;
            }

            _cache.MarkRevoked(userId, sessionId, RevokedSessionCacheDuration);
            return true;
        }
    }
}
