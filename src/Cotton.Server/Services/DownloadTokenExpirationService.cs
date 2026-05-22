// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services;

public sealed class DownloadTokenExpirationService(CottonDbContext _dbContext)
{
    private const int BatchSize = 1_000;

    public async Task<int> ExpireActiveTokensCreatedByUserAsync(
        Guid userId,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        int expired = 0;
        while (true)
        {
            List<DownloadToken> tokens = await _dbContext.DownloadTokens
                .Where(x => x.CreatedByUserId == userId
                    && (x.ExpiresAt == null || x.ExpiresAt > expiresAt))
                .OrderBy(x => x.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (tokens.Count == 0)
            {
                return expired;
            }

            foreach (DownloadToken token in tokens)
            {
                token.ExpiresAt = expiresAt;
            }

            expired += tokens.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
        }
    }
}
