// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Revokes active push device tokens that have not refreshed within the stale-token window.
    /// </summary>
    [JobTrigger(days: 1)]
    public class PushDeviceTokenStaleCleanupJob(
        CottonDbContext _dbContext,
        ILogger<PushDeviceTokenStaleCleanupJob> _logger) : IJob
    {
        private const int BatchSize = 500;
        private const int StaleTokenAgeDays = 30;

        /// <summary>
        /// Executes the scheduled Quartz job.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken ct = context.CancellationToken;
            DateTime revokedAt = DateTime.UtcNow;
            DateTime cutoff = revokedAt.AddDays(-StaleTokenAgeDays);
            int revokedTokens = 0;

            while (true)
            {
                List<PushDeviceToken> tokens = await _dbContext.PushDeviceTokens
                    .Where(x => x.RevokedAt == null && x.LastRegisteredAt < cutoff)
                    .OrderBy(x => x.LastRegisteredAt)
                    .ThenBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (tokens.Count == 0)
                {
                    break;
                }

                foreach (PushDeviceToken token in tokens)
                {
                    token.RevokedAt = revokedAt;
                }

                revokedTokens += tokens.Count;
                await _dbContext.SaveChangesAsync(ct);
                _dbContext.ChangeTracker.Clear();
            }

            _logger.LogInformation(
                "Revoked {RevokedTokenCount} stale push device token(s) last registered before {CutoffUtc}.",
                revokedTokens,
                cutoff);
        }
    }
}
