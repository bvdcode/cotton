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
    /// Backfills legacy null metadata dictionaries to empty dictionaries.
    /// </summary>
    [JobTrigger(days: 1)]
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
        }

        private async Task<int> BackfillNodesAsync(CancellationToken ct)
        {
            int totalUpdated = 0;

            while (true)
            {
                List<Node> nodes = await _dbContext.Nodes
                    .Where(x => x.Metadata == null)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (nodes.Count == 0)
                {
                    return totalUpdated;
                }

                foreach (Node? node in nodes)
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
                List<NodeFile> nodeFiles = await _dbContext.NodeFiles
                    .Where(x => x.Metadata == null)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (nodeFiles.Count == 0)
                {
                    return totalUpdated;
                }

                foreach (NodeFile? nodeFile in nodeFiles)
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
