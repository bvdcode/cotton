// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Runs scheduled sync change retention maintenance.
    /// </summary>
    [JobTrigger(days: 1)]
    public class SyncChangeRetentionJob(
        CottonDbContext _dbContext,
        ILogger<SyncChangeRetentionJob> _logger) : IJob
    {
        private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(365);

        /// <summary>
        /// Executes the scheduled Quartz job.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            DateTime cutoff = DateTime.UtcNow - RetentionPeriod;
            int deletedCount = await _dbContext.SyncChanges
                .Where(x => x.CreatedAt < cutoff)
                .ExecuteDeleteAsync(context.CancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Deleted {DeletedCount} sync changes older than {Cutoff}.",
                    deletedCount,
                    cutoff);
            }
        }
    }
}
