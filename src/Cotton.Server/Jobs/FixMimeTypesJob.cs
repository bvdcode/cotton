using Cotton.Database;
using EasyExtensions.Quartz.Attributes;
using Microsoft.AspNetCore.StaticFiles;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class FixMimeTypesJob(CottonDbContext _dbContext, ILogger<FixMimeTypesJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            FileExtensionContentTypeProvider provider = new();
            var manifestsToFix = _dbContext.FileManifests.Where(m => m.ContentType == "application/octet-stream").ToList();
            foreach (var manifest in manifestsToFix)
            {
                var extension = Path.GetExtension(manifest.NodeFiles.First().Name);
                if (provider.TryGetContentType(extension, out var contentType))
                {
                    manifest.ContentType = contentType;
                    _logger.LogInformation("Updated content type for manifest {ManifestId} to {ContentType}", manifest.Id, contentType);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
