// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Extensions
{
    internal static class DownloadTokenExtensions
    {
        public static async Task<DownloadToken?> FindActiveAsync(
            this DbSet<DownloadToken> downloadTokens,
            string token,
            Guid nodeFileId,
            CancellationToken ct = default)
        {
            DownloadToken? downloadToken = await downloadTokens
                .FirstOrDefaultAsync(
                    x => x.Token == token && x.NodeFileId == nodeFileId,
                    ct);

            if (downloadToken?.ExpiresAt is DateTime expiresAt && expiresAt < DateTime.UtcNow)
            {
                return null;
            }

            return downloadToken;
        }

        public static void RegisterDeleteAfterUse(
            this HttpResponse response,
            CottonDbContext dbContext,
            DownloadToken downloadToken)
        {
            if (!downloadToken.DeleteAfterUse)
            {
                return;
            }

            response.OnCompleted(() => DeleteAsync(dbContext, downloadToken));
        }

        public static void RegisterDeleteAfterUse(
            this HttpResponse response,
            CottonDbContext dbContext,
            Guid tokenId)
        {
            response.OnCompleted(async () =>
            {
                DownloadToken? token = await dbContext.DownloadTokens.FirstOrDefaultAsync(x => x.Id == tokenId);
                if (token is not null)
                {
                    await DeleteAsync(dbContext, token);
                }
            });
        }

        private static async Task DeleteAsync(CottonDbContext dbContext, DownloadToken token)
        {
            dbContext.DownloadTokens.Remove(token);
            await dbContext.SaveChangesAsync();
        }
    }
}
