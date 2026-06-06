// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Backfills legacy null metadata dictionaries to empty dictionaries.
    /// </summary>
    [JobTrigger(days: 1)]
    [DisallowConcurrentExecution]
    public class BackfillNullMetadataJob(
        CottonDbContext _dbContext,
        ILogger<BackfillNullMetadataJob> _logger) : IJob
    {
        private const int BatchSize = 1000;

        /// <summary>
        /// Executes the scheduled Quartz job.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            // TEMPORARY: remove this job after the null metadata cleanup has rolled out to all deployed databases.
            CancellationToken ct = context.CancellationToken;

            int updatedNodes = await BackfillNodesAsync(ct);
            int updatedNodeFiles = await BackfillNodeFilesAsync(ct);

            _logger.LogInformation(
                "Backfilled null metadata values. Nodes: {UpdatedNodes}; node files: {UpdatedNodeFiles}.",
                updatedNodes,
                updatedNodeFiles);

            bool deleted = await context.Scheduler.DeleteJob(context.JobDetail.Key, ct);
            if (deleted)
            {
                _logger.LogInformation(
                    "Temporary null metadata backfill job removed from the current Quartz scheduler.");
            }
        }

        private async Task<int> BackfillNodesAsync(CancellationToken ct)
        {
            int totalUpdated = 0;

            while (true)
            {
                var nodes = await _dbContext.Nodes
                    .Where(x => x.Metadata == null)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (nodes.Count == 0)
                {
                    return totalUpdated;
                }

                foreach (var node in nodes)
                {
                    node.Metadata = [];
                }

                totalUpdated += nodes.Count;
                await _dbContext.SaveChangesAsync(ct);
                _dbContext.ChangeTracker.Clear();
            }
        }

        private async Task<int> BackfillNodeFilesAsync(CancellationToken ct)
        {
            int totalUpdated = 0;

            while (true)
            {
                var nodeFiles = await _dbContext.NodeFiles
                    .Where(x => x.Metadata == null)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (nodeFiles.Count == 0)
                {
                    return totalUpdated;
                }

                foreach (var nodeFile in nodeFiles)
                {
                    nodeFile.Metadata = [];
                }

                totalUpdated += nodeFiles.Count;
                await _dbContext.SaveChangesAsync(ct);
                _dbContext.ChangeTracker.Clear();
            }
        }
    }
}
