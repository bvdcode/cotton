using Cotton.Database;
using Cotton.Previews;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 1)]
    public class GeneratePreviewJob(
        IStreamCipher _crypto,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        ILogger<GeneratePreviewJob> _logger) : IJob
    {
        private const int MaxItemsPerRun = 1000;

        public async Task Execute(IJobExecutionContext context)
        {
            var allSupportedMimeTypes = PreviewGeneratorProvider.GetAllSupportedMimeTypes();
            _logger.LogInformation("Starting preview generation job. Supported mime types: {MimeTypes}", string.Join(", ", allSupportedMimeTypes));

            var itemsToProcess = _dbContext.FileManifests
                .Where(fm => fm.EncryptedFilePreviewHash == null)
                .Where(fm => allSupportedMimeTypes.Contains(fm.ContentType))
                .Include(fm => fm.FileManifestChunks)
                .ThenInclude(fmc => fmc.Chunk)
                .OrderBy(fm => fm.CreatedAt)
                .Take(MaxItemsPerRun)
                .ToList();

            if (itemsToProcess.Count > 0)
            {
                _logger.LogInformation("Generating previews for {Count} file manifests", itemsToProcess.Count);
            }

            int processed = 0;
            foreach (var item in itemsToProcess)
            {
                processed++;
                _logger.LogInformation("[Job] Processing {Current}/{Total}: FileManifest {FileManifestId}, ContentType={ContentType}, Size={Size}",
                    processed, itemsToProcess.Count, item.Id, item.ContentType, item.SizeBytes);

                var generator = PreviewGeneratorProvider.GetGeneratorByContentType(item.ContentType);
                if (generator == null)
                {
                    _logger.LogWarning("No preview generator found for content type {ContentType}", item.ContentType);
                    continue;
                }

                PipelineContext pipelineContext = new()
                {
                    FileSizeBytes = item.SizeBytes,
                    ChunkLengths = item.FileManifestChunks.GetChunkLengths()
                };
                var uids = item.FileManifestChunks.GetChunkHashes();

                try
                {
                    _logger.LogInformation("[Job] Getting blob stream for FileManifest {FileManifestId}...", item.Id);
                    await using var fs = _storage.GetBlobStream(uids, pipelineContext);

                    _logger.LogInformation("[Job] Calling GeneratePreviewWebPAsync for FileManifest {FileManifestId}...", item.Id);
                    var previewImage = await generator.GeneratePreviewWebPAsync(fs);

                    byte[] hash = Hasher.HashData(previewImage);
                    string hashStr = Hasher.ToHexStringHash(hash);

                    _logger.LogInformation("[Job] Storing preview (hash={Hash}) for FileManifest {FileManifestId}...", hashStr, item.Id);
                    using var resultStream = new MemoryStream(previewImage);
                    await _storage.WriteAsync(hashStr, resultStream);

                    item.EncryptedFilePreviewHash = _crypto.Encrypt(hashStr);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation("Generated preview for file manifest {FileManifestId}", item.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate preview for file manifest {FileManifestId}", item.Id);

                    // TODO: For debugging purposes only
                    throw;
                }
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("[Job] Preview generation job completed successfully. Processed {Count} items", processed);
        }
    }
}
