using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class CollectPerformanceJob(ILogger<CollectPerformanceJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("CollectPerformanceJob started at {Time}", DateTimeOffset.Now);
        }
    }
}
