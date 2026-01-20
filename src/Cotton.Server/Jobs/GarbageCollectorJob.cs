using Cotton.Database;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class GarbageCollectorJob(
        CottonDbContext _dbContext,
        ILogger<GarbageCollectorJob> _logger) : IJob
    {
        private const int BatchSize = 1000;
        private const int ChunkGcDelayDays = 7;

        public async Task Execute(IJobExecutionContext context)
        {
            var manifestIds = await _dbContext.FileManifests
                .Where(fm => !fm.NodeFiles.Any())
                .OrderBy(fm => fm.Id)
                .Select(fm => fm.Id)
                .Take(BatchSize)
                .ToListAsync(context.CancellationToken);

            if (manifestIds.Count != 0)
            {
                await _dbContext.FileManifestChunks
                    .Where(m => manifestIds.Contains(m.FileManifestId))
                    .ExecuteDeleteAsync(context.CancellationToken);

                int deletedManifests = await _dbContext.FileManifests
                    .Where(fm => manifestIds.Contains(fm.Id))
                    .ExecuteDeleteAsync(context.CancellationToken);

                _logger.LogInformation("Removed {Count} orphaned file manifests.", deletedManifests);
            }

            DateTime deleteAfter = DateTime.UtcNow.AddDays(ChunkGcDelayDays);
            int orphanedChunks = await _dbContext.Chunks
                .Where(c => !c.FileManifestChunks.Any() && c.GCScheduledAfter == null)
                .Take(BatchSize)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, deleteAfter), context.CancellationToken);
            if (orphanedChunks != 0)
            {
                _logger.LogInformation("Scheduled {Count} orphaned chunks for garbage collection.", orphanedChunks);
            }
        }
    }
}
