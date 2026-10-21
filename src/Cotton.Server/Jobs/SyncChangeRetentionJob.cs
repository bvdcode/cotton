// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Extensions;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class SyncChangeRetentionJob(
        CottonDbContext _dbContext,
        ILogger<SyncChangeRetentionJob> _logger) : IJob
    {
        private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

        public async Task Execute(IJobExecutionContext context)
        {
            DateTime cutoff = DateTime.UtcNow - RetentionPeriod;
            int deletedCount = await DeleteExpiredChangesAsync(cutoff, context.CancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Deleted {DeletedCount} sync changes older than {Cutoff}.",
                    deletedCount,
                    cutoff);
            }
        }

        internal Task<int> DeleteExpiredChangesAsync(DateTime cutoff, CancellationToken cancellationToken)
        {
            return _dbContext.SyncChanges.DeleteExpiredSyncChangesAsync(cutoff, cancellationToken);
        }
    }
}
