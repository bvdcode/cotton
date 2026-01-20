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
        private const int BatchSize = 10000;

        public async Task Execute(IJobExecutionContext context)
        {
            var orphanedManifests = await _dbContext.FileManifests
                .Where(fm => !fm.NodeFiles.Any())
                .Take(BatchSize)
                .ToListAsync();
            if (orphanedManifests.Count != 0)
            {
                _dbContext.FileManifests.RemoveRange(orphanedManifests);
                int removed = await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Removed {Count} orphaned file manifests.", removed);
            }
        }
    }
}
