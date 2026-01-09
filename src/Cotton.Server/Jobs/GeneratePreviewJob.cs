using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
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
            var allSupportedMimeTypes = PreviewGeneratorProvider.GetAllSupportedMimeTypes();
            var itemsToProcess = _dbContext.FileManifests
                .Where(fm => fm.FilePreviewId == null)
                .Where(fm => allSupportedMimeTypes.Contains(fm.ContentType))
                .Include(fm => fm.FileManifestChunks)
                .OrderBy(fm => fm.CreatedAt)
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
                byte[] hash = Hasher.HashData(previewImage);
                var existing = await _dbContext.FilePreviews.FirstOrDefaultAsync(fp => fp.Hash == hash);
                if (existing != null)
                {
                    item.FilePreviewId = existing.Id;
                    _logger.LogInformation("Reused existing preview for file manifest {FileManifestId}", item.Id);
                    continue;
                }
                string hashStr = Hasher.ToHexStringHash(hash);
                using var resultStream = new MemoryStream(previewImage);
                await _storage.WriteAsync(hashStr, resultStream);
                FilePreview preview = new()
                {
                    Hash = hash
                };
                await _dbContext.FilePreviews.AddAsync(preview);
                item.FilePreview = preview;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Generated preview for file manifest {FileManifestId}", item.Id);
            }
            await _dbContext.SaveChangesAsync();
        }
    }
}
