using Cotton.Database;
using EasyExtensions.Quartz.Attributes;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 1)]
    public class GeneratePreviewJob(CottonDbContext _dbContext) : IJob
    {
        private const int MaxItemsPerRun = 100;

        public async Task Execute(IJobExecutionContext context)
        {
            // Placeholder implementation
            var itemsToProcess = _dbContext.FileManifests
                .Where(fm => fm.PreviewImageHash == null)
                .Take(MaxItemsPerRun)
                .ToList();
            foreach (var item in itemsToProcess)
            {
                // Simulate preview generation
                await Task.Delay(1000);
            }
            await _dbContext.SaveChangesAsync();
        }
    }
}
