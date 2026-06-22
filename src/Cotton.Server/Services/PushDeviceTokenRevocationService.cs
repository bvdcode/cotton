// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates server-side push device token revocation.
    /// </summary>
    public class PushDeviceTokenRevocationService(CottonDbContext _dbContext)
    {
        /// <summary>
        /// Revokes active push device tokens registered by a single auth session.
        /// </summary>
        public Task<int> RevokeSessionAsync(
            Guid userId,
            string sessionId,
            DateTime revokedAt,
            CancellationToken cancellationToken)
        {
            return RevokeAsync(
                _dbContext.PushDeviceTokens
                    .Where(x => x.UserId == userId
                        && x.SessionId == sessionId
                        && x.RevokedAt == null),
                revokedAt,
                cancellationToken);
        }

        /// <summary>
        /// Revokes every active push device token owned by the user.
        /// </summary>
        public Task<int> RevokeUserTokensAsync(
            Guid userId,
            DateTime revokedAt,
            CancellationToken cancellationToken)
        {
            return RevokeAsync(
                _dbContext.PushDeviceTokens
                    .Where(x => x.UserId == userId && x.RevokedAt == null),
                revokedAt,
                cancellationToken);
        }

        private static Task<int> RevokeAsync(
            IQueryable<PushDeviceToken> query,
            DateTime revokedAt,
            CancellationToken cancellationToken)
        {
            return query.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, revokedAt)
                    .SetProperty(token => token.UpdatedAt, revokedAt),
                cancellationToken);
        }
    }
}
