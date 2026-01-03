using Cotton.Server.Services;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class CollectPerformanceJob(
        PerfTracker _perf,
        ILogger<CollectPerformanceJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            if (_perf.IsUploading())
            {
                _logger.LogInformation("Performance data collection skipped: upload in progress.");
                return;
            }
            _logger.LogInformation("CollectPerformanceJob started at {Time}", DateTimeOffset.Now);
        }
    }
}
