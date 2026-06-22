// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Runs the scheduled download token retention maintenance task.
    /// </summary>
    [JobTrigger(days: 1)]
    public class DownloadTokenRetentionJob(
        CottonDbContext _dbContext,
        ILogger<DownloadTokenRetentionJob> _logger) : IJob
    {
        /// <summary>
        /// Executes the scheduled Quartz job.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(240_000); // Wait for 4 minutes for the server to start up and stabilize

            DateTime now = DateTime.UtcNow;
            DateTime removalThreshold = now.AddDays(-30);
            List<DownloadToken> expiredTokens = await _dbContext.DownloadTokens
                .Where(dt => dt.ExpiresAt != null && dt.ExpiresAt <= removalThreshold)
                .ToListAsync();
            if (expiredTokens.Count == 0)
            {
                return;
            }
            _dbContext.DownloadTokens.RemoveRange(expiredTokens);
            int deletedCount = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Deleted {DeletedCount} expired download tokens", deletedCount);
        }
    }
}
