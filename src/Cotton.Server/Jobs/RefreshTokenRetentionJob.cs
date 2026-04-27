using Cotton.Database;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class RefreshTokenRetentionJob(
        CottonDbContext _dbContext,
        ILogger<RefreshTokenRetentionJob> _logger) : IJob
    {
        private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(600_000); // Wait for 10 minutes for the server to start up and stabilize

            var cutoffDate = DateTime.UtcNow - RetentionPeriod;
            var tokensToRefresh = _dbContext.RefreshTokens
                .Where(rt => rt.CreatedAt < cutoffDate)
                .ToList();
            foreach (var token in tokensToRefresh)
            {
                token.RevokedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Revoked {Count} refresh tokens older than {CutoffDate}", tokensToRefresh.Count, cutoffDate);
        }
    }
}
