using Cotton.Database;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class DownloadTokenRetentionJob(
        CottonDbContext _dbContext,
        ILogger<DownloadTokenRetentionJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(240_000); // Wait for 4 minutes for the server to start up and stabilize

            DateTime now = DateTime.UtcNow;
            DateTime removalThreshold = now.AddDays(-30);
            var expiredTokens = await _dbContext.DownloadTokens
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
