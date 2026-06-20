// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Runs the scheduled refresh token retention maintenance task.
    /// </summary>
    [JobTrigger(days: 1)]
    public class RefreshTokenRetentionJob(
        CottonDbContext _dbContext,
        ILogger<RefreshTokenRetentionJob> _logger) : IJob
    {
        private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

        /// <summary>
        /// Executes the scheduled Quartz job.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(600_000); // Wait for 10 minutes for the server to start up and stabilize

            var cutoffDate = DateTime.UtcNow - RetentionPeriod;
            var tokensToRefresh = await _dbContext.RefreshTokens
                .Where(rt => rt.RevokedAt == null)
                .Where(rt => rt.CreatedAt < cutoffDate)
                .ToListAsync(context.CancellationToken);
            foreach (var token in tokensToRefresh)
            {
                token.RevokedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Revoked {Count} refresh tokens older than {CutoffDate}", tokensToRefresh.Count, cutoffDate);
        }
    }
}
