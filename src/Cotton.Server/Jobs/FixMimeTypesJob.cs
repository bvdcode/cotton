using Cotton.Database;
using EasyExtensions.Quartz.Attributes;
using Microsoft.AspNetCore.StaticFiles;
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
            FileExtensionContentTypeProvider provider = new();
            long totalUpdated = 0;
            Guid lastId = Guid.Empty;

            while (!context.CancellationToken.IsCancellationRequested)
            {
                var manifests = await _dbContext.FileManifests
                    .Include(m => m.NodeFiles)
                    .Where(m =>
                        m.ContentType == "application/octet-stream" &&
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

                    var extension = Path.GetExtension(fileName);
                    if (string.IsNullOrWhiteSpace(extension))
                        continue;

                    if (!provider.TryGetContentType(extension, out var contentType))
                        continue;

                    if (manifest.ContentType != contentType)
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