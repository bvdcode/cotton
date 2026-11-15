// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Abstractions;
using Cotton.Server.Services;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Cotton.Server.Extensions
{
    internal static class PublicShareTokenExtensions
    {
        public static async Task<bool> TryCreatePublicShareTokenAsync<T>(
            this CottonDbContext database,
            T token,
            CancellationToken ct) where T : BaseEntity<Guid>, IPublicShareToken
        {
            PublicShareExecutionStrategy strategy = new(database);
            bool created = await strategy.ExecuteInTransactionAsync(
                async cancellationToken =>
                {
                    DateTime now = DateTime.UtcNow;
                    bool exists = await database.DownloadTokens.AnyAsync(
                        x => x.Token == token.Token && (x.ExpiresAt == null || x.ExpiresAt > now), cancellationToken)
                        || await database.NodeShareTokens.AnyAsync(
                            x => x.Token == token.Token && (x.ExpiresAt == null || x.ExpiresAt > now), cancellationToken);
                    if (exists)
                    {
                        return false;
                    }

                    database.Add(token);
                    await database.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
                    return true;
                },
                cancellationToken => database.Set<T>().AnyAsync(x => x.Id == token.Id, cancellationToken),
                IsolationLevel.Serializable,
                ct);
            if (created)
            {
                database.ChangeTracker.AcceptAllChanges();
            }
            else
            {
                database.Entry(token).State = EntityState.Detached;
            }
            return created;
        }
    }
}
