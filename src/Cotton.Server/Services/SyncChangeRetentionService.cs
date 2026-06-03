// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services;

/// <summary>Prunes old sync changes according to configured retention.</summary>
public sealed class SyncChangeRetentionService(
    CottonDbContext _dbContext,
    IOptions<SyncChangeRetentionOptions> _options)
{
    /// <summary>Deletes expired sync changes for one user.</summary>
    public async Task<int> DeleteExpiredChangesAsync(Guid userId, CancellationToken ct = default)
    {
        int retentionDays = Math.Max(
            SyncChangeRetentionOptions.MinimumRetentionDays,
            _options.Value.RetentionDays);
        DateTime cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        return await _dbContext.SyncChanges
            .Where(x => x.OwnerId == userId && x.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
