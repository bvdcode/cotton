// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

/// <summary>
/// Coordinates refresh token revocation.
/// </summary>
public sealed class RefreshTokenRevocationService(CottonDbContext _dbContext)
{
    private const int BatchSize = 1_000;

    /// <summary>
    /// Revokes session.
    /// </summary>
    public Task<int> RevokeSessionAsync(
        Guid userId,
        string sessionId,
        DateTime revokedAt,
        CancellationToken cancellationToken)
    {
        return RevokeAsync(
            _dbContext.RefreshTokens
                .Where(x => x.UserId == userId && x.SessionId == sessionId && x.RevokedAt == null),
            revokedAt,
            cancellationToken);
    }

    /// <summary>
    /// Revokes user sessions.
    /// </summary>
    public Task<int> RevokeUserSessionsAsync(
        Guid userId,
        DateTime revokedAt,
        CancellationToken cancellationToken)
    {
        return RevokeAsync(
            _dbContext.RefreshTokens
                .Where(x => x.UserId == userId && x.RevokedAt == null),
            revokedAt,
            cancellationToken);
    }

    private async Task<int> RevokeAsync(
        IQueryable<ExtendedRefreshToken> query,
        DateTime revokedAt,
        CancellationToken cancellationToken)
    {
        int revoked = 0;
        while (true)
        {
            List<ExtendedRefreshToken> tokens = await query
                .OrderBy(x => x.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (tokens.Count == 0)
            {
                return revoked;
            }

            foreach (ExtendedRefreshToken token in tokens)
            {
                token.RevokedAt = revokedAt;
            }

            revoked += tokens.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
        }
    }
}
