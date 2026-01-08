using Cotton.Database;
using Cotton.Previews;
using Cotton.Server.Extensions;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 1)]
    public class GeneratePreviewJob(
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        ILogger<GeneratePreviewJob> _logger) : IJob
    {
        private const int MaxItemsPerRun = 100;

        public async Task Execute(IJobExecutionContext context)
        {
            // Placeholder implementation
            var itemsToProcess = _dbContext.FileManifests
                .Where(fm => fm.PreviewImageHash == null)
                .Include(fm => fm.FileManifestChunks)
                .Take(MaxItemsPerRun)
                .ToList();
            foreach (var item in itemsToProcess)
            {
                var generator = PreviewGeneratorProvider.GetGeneratorByContentType(item.ContentType);
                if (generator == null)
                {
                    _logger.LogWarning("No preview generator found for content type {ContentType}", item.ContentType);
                    continue;
                }
                PipelineContext pipelineContext = new()
                {
                    FileSizeBytes = item.SizeBytes
                };
                var uids = item.FileManifestChunks.GetChunkHashes();
                var fs = _storage.GetBlobStream(uids, pipelineContext);
                var previewImage = await generator.GeneratePreviewWebPAsync(fs);
            }
            await _dbContext.SaveChangesAsync();
        }
    }
}
