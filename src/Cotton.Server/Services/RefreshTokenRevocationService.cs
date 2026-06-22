// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates refresh token revocation.
    /// </summary>
    public class RefreshTokenRevocationService(
        CottonDbContext _dbContext,
        PushDeviceTokenRevocationService _pushDeviceTokenRevocations)
    {
        private const int BatchSize = 1_000;

        /// <summary>
        /// Revokes session.
        /// </summary>
        public async Task<RefreshTokenRevocationResult> RevokeSessionAsync(
            Guid userId,
            string sessionId,
            DateTime revokedAt,
            CancellationToken cancellationToken)
        {
            RefreshTokenRevocationResult result = await RevokeAsync(
                _dbContext.RefreshTokens
                    .Where(x => x.UserId == userId && x.SessionId == sessionId && x.RevokedAt == null),
                revokedAt,
                cancellationToken);
            int revokedPushDeviceTokens = await _pushDeviceTokenRevocations.RevokeSessionTokensAsync(
                userId,
                sessionId,
                revokedAt,
                cancellationToken);

            IReadOnlyList<string> sessionIds = result.SessionIds.Contains(sessionId, StringComparer.Ordinal)
                ? result.SessionIds
                : [.. result.SessionIds, sessionId];
            return result with
            {
                SessionIds = sessionIds,
                RevokedPushDeviceTokens = revokedPushDeviceTokens
            };
        }

        /// <summary>
        /// Revokes user sessions.
        /// </summary>
        public async Task<RefreshTokenRevocationResult> RevokeUserSessionsAsync(
            Guid userId,
            DateTime revokedAt,
            CancellationToken cancellationToken)
        {
            RefreshTokenRevocationResult result = await RevokeAsync(
                _dbContext.RefreshTokens
                    .Where(x => x.UserId == userId && x.RevokedAt == null),
                revokedAt,
                cancellationToken);
            int revokedPushDeviceTokens = await _pushDeviceTokenRevocations.RevokeUserTokensAsync(
                userId,
                revokedAt,
                cancellationToken);

            return result with
            {
                RevokedPushDeviceTokens = revokedPushDeviceTokens
            };
        }

        private async Task<RefreshTokenRevocationResult> RevokeAsync(
            IQueryable<ExtendedRefreshToken> query,
            DateTime revokedAt,
            CancellationToken cancellationToken)
        {
            int revoked = 0;
            var sessionIds = new List<string>();
            var seenSessionIds = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                List<ExtendedRefreshToken> tokens = await query
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);
                if (tokens.Count == 0)
                {
                    return new RefreshTokenRevocationResult(
                        revoked,
                        sessionIds,
                        RevokedPushDeviceTokens: 0);
                }

                foreach (ExtendedRefreshToken token in tokens)
                {
                    if (!string.IsNullOrWhiteSpace(token.SessionId) && seenSessionIds.Add(token.SessionId))
                    {
                        sessionIds.Add(token.SessionId);
                    }

                    token.RevokedAt = revokedAt;
                }

                revoked += tokens.Count;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
            }
        }
    }
}
