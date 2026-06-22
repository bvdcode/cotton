// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Stores session access token revocation state.
    /// </summary>
    public class SessionAccessTokenRevocationStore(
        CottonDbContext _dbContext,
        SessionAccessTokenRevocationCache _cache,
        IDatabaseIntegrityVerifier _integrity)
    {
        private static readonly TimeSpan ActiveSessionCacheDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RevokedSessionCacheDuration = TimeSpan.FromMinutes(65);
        private static readonly TimeSpan AccessTokenClockSkew = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Creates a revocation marker for the current session.
        /// </summary>
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

        /// <summary>
        /// Indicates whether revoked async.
        /// </summary>
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

            List<ExtendedRefreshToken> activeRefreshTokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId
                    && x.SessionId == sessionId
                    && x.RevokedAt == null)
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (ExtendedRefreshToken refreshToken in activeRefreshTokens)
            {
                _integrity.RequireValid(_dbContext, refreshToken, "auth.access-token-session");
            }

            if (activeRefreshTokens.Count > 0)
            {
                _cache.MarkActive(userId, sessionId, ActiveSessionCacheDuration);
                return false;
            }

            _cache.MarkRevoked(userId, sessionId, RevokedSessionCacheDuration);
            return true;
        }
    }
}
