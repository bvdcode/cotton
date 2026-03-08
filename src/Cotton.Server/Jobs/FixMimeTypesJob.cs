using Cotton.Database;
using Cotton.Server.Services;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class FixMimeTypesJob(
        CottonDbContext _dbContext,
        ILogger<FixMimeTypesJob> _logger) : IJob
    {
        private const int BatchSize = 1000;

        public async Task Execute(IJobExecutionContext context)
        {
            long totalUpdated = 0;
            Guid lastId = Guid.Empty;

            while (!context.CancellationToken.IsCancellationRequested)
            {
                var manifests = await _dbContext.FileManifests
                    .Include(m => m.NodeFiles)
                    .Where(m =>
                        (m.ContentType == FileManifestService.DefaultContentType
                            || m.ContentType == string.Empty) &&
                        m.Id.CompareTo(lastId) > 0)
                    .OrderBy(m => m.Id)
                    .Take(BatchSize)
                    .ToListAsync(context.CancellationToken);

                if (manifests.Count == 0)
                    break;

                foreach (var manifest in manifests)
                {
                    var fileName = manifest.NodeFiles.FirstOrDefault()?.Name;
                    if (string.IsNullOrWhiteSpace(fileName))
                        continue;

                    string contentType = FileManifestService.ResolveContentType(fileName, manifest.ContentType);
                    if (!string.Equals(manifest.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
                    {
                        manifest.ContentType = contentType;
                        totalUpdated++;
                    }
                }

                await _dbContext.SaveChangesAsync(context.CancellationToken);

                lastId = manifests[^1].Id;
                _dbContext.ChangeTracker.Clear();

                _logger.LogInformation(
                    "Processed up to manifest {LastId}. Total updated: {TotalUpdated}",
                    lastId,
                    totalUpdated);
            }

            _logger.LogInformation("FixMimeTypesJob completed. Total updated: {TotalUpdated}", totalUpdated);
        }
    }
}