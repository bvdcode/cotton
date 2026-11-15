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
            DateTime now = DateTime.UtcNow;
            return await downloadTokens.SingleOrDefaultAsync(
                x => x.Token == token && x.NodeFileId == nodeFileId
                    && (x.ExpiresAt == null || x.ExpiresAt > now),
                ct);
        }

        public static void RegisterDeleteAfterUse(
            this HttpResponse response,
            CottonDbContext dbContext,
            DownloadToken? downloadToken)
        {
            if (downloadToken is null || !downloadToken.DeleteAfterUse)
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
