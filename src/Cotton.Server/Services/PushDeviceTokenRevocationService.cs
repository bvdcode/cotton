// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates push device token revocation for auth session lifecycle events.
    /// </summary>
    public class PushDeviceTokenRevocationService(CottonDbContext _dbContext)
    {
        private const int BatchSize = 1_000;

        /// <summary>
        /// Revokes active push device tokens registered by one user session.
        /// </summary>
        public Task<int> RevokeSessionTokensAsync(
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
        /// Revokes all active push device tokens owned by the user.
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

        private async Task<int> RevokeAsync(
            IQueryable<PushDeviceToken> query,
            DateTime revokedAt,
            CancellationToken cancellationToken)
        {
            int revoked = 0;
            while (true)
            {
                List<PushDeviceToken> tokens = await query
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);
                if (tokens.Count == 0)
                {
                    return revoked;
                }

                foreach (PushDeviceToken token in tokens)
                {
                    token.RevokedAt = revokedAt;
                }

                revoked += tokens.Count;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
            }
        }
    }
}
