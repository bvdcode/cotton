using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Extensions;
using Cotton.Server.Hubs;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(minutes: 15)]
    public class GeneratePreviewJob(
        PerfTracker _perf,
        IStreamCipher _crypto,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        IHubContext<EventHub> _hubContext,
        ILogger<GeneratePreviewJob> _logger) : IJob
    {
        private const int MaxItemsPerRun = 1000;

        public async Task Execute(IJobExecutionContext context)
        {
            var allSupportedMimeTypes = PreviewGeneratorProvider.GetAllSupportedMimeTypes();
            var generatorVersionsByContentType = PreviewGeneratorProvider.GetGeneratorVersionsByContentType();

            var processableItemsQuery = _dbContext.FileManifests
                .Where(fm => allSupportedMimeTypes.Contains(fm.ContentType));

            var itemsToProcessQuery = processableItemsQuery
                .Where(fm => fm.SmallFilePreviewHash == null)
                .Where(fm => fm.PreviewGenerationError == null);

            foreach (var versionGroup in generatorVersionsByContentType.GroupBy(x => x.Value))
            {
                int generatorVersion = versionGroup.Key;
                string[] contentTypes = [.. versionGroup.Select(x => x.Key)];

                itemsToProcessQuery = itemsToProcessQuery
                    .Union(processableItemsQuery
                        .Where(fm => contentTypes.Contains(fm.ContentType))
                        .Where(fm => fm.PreviewGeneratorVersion != generatorVersion));
            }

            var itemIds = await itemsToProcessQuery
                .OrderBy(fm => fm.CreatedAt)
                .Select(fm => fm.Id)
                .Take(MaxItemsPerRun)
                .ToListAsync();

            var itemsToProcess = await _dbContext.FileManifests
                .Where(fm => itemIds.Contains(fm.Id))
                .Include(fm => fm.NodeFiles)
                .Include(fm => fm.FileManifestChunks)
                .ThenInclude(fmc => fmc.Chunk)
                .ToListAsync();

            var itemsToProcessById = itemsToProcess.ToDictionary(x => x.Id);
            itemsToProcess = [.. itemIds.Where(itemsToProcessById.ContainsKey).Select(id => itemsToProcessById[id])];

            if (itemsToProcess.Count > 0)
            {
                _logger.LogInformation("Generating previews for {Count} file manifests", itemsToProcess.Count);
            }

            int processed = 0;
            foreach (var item in itemsToProcess)
            {
                processed++;
                _logger.LogInformation("Processing {Current}/{Total}: FileManifest {FileManifestId}, ContentType={ContentType}, Size={Size}",
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
                    _logger.LogDebug("Getting blob stream for FileManifest {FileManifestId}...", item.Id);
                    await using var fsSmall = _storage.GetBlobStream(uids, pipelineContext);
                    byte[] previewImage = await generator.GeneratePreviewWebPAsync(fsSmall, PreviewGeneratorProvider.DefaultSmallPreviewSize);
                    byte[] hash = Hasher.HashData(previewImage);
                    string hashStr = Hasher.ToHexStringHash(hash);
                    _logger.LogDebug("Storing preview (hash={Hash}) for FileManifest {FileManifestId}...", hashStr, item.Id);
                    using var resultStream = new MemoryStream(previewImage);
                    await _storage.WriteAsync(hashStr, resultStream);
                    await EnsureChunkExistsAsync(hash, previewImage.Length);
                    item.SmallFilePreviewHash = hash;
                    item.SmallFilePreviewHashEncrypted = _crypto.Encrypt(hash);
                    item.PreviewGenerationError = null;
                    item.PreviewGeneratorVersion = generator.Version;

                    if (generator is ImagePreviewGenerator or HeicPreviewGenerator)
                    {
                        await using var fsLarge = _storage.GetBlobStream(uids, pipelineContext);
                        byte[] previewImageLarge = await generator.GeneratePreviewWebPAsync(fsLarge, PreviewGeneratorProvider.DefaultLargePreviewSize);
                        byte[] hashLarge = Hasher.HashData(previewImageLarge);
                        string hashLargeStr = Hasher.ToHexStringHash(hashLarge);
                        _logger.LogDebug("Storing large preview (hash={Hash}) for FileManifest {FileManifestId}...", hashLargeStr, item.Id);
                        using var resultStreamLarge = new MemoryStream(previewImageLarge);
                        await _storage.WriteAsync(hashLargeStr, resultStreamLarge);
                        await EnsureChunkExistsAsync(hashLarge, previewImageLarge.Length);
                        item.LargeFilePreviewHash = hashLarge;
                    }

                    await _dbContext.SaveChangesAsync();
                    _logger.LogDebug("Generated preview for file manifest {FileManifestId}", item.Id);
                    foreach (var nodeFile in item.NodeFiles)
                    {
                        // Minor vulnerability:
                        // Even if cross-user deduplication is disabled, this event could reveal to a user who already had the file that someone else had the file,
                        // because the preview hash will be reset and regenerated, preventing the second user from discovering that the first user had the file.
                        await _hubContext.Clients
                            .User(nodeFile.OwnerId.ToString())
                            .SendAsync("PreviewGenerated", nodeFile.NodeId, nodeFile.Id, item.GetPreviewHashEncryptedHex());
                    }
                    // TODO: Move to settings or autoconfig
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate preview for file manifest {FileManifestId}", item.Id);
                    item.PreviewGenerationError = ex.Message;
                    item.PreviewGeneratorVersion = generator.Version;
                    await _dbContext.SaveChangesAsync();
                }

                if (_perf.IsUploading())
                {
                    const int waitTimeSeconds = 5;
                    _logger.LogInformation("Upload in progress, waiting {seconds}s before processing next item...", waitTimeSeconds);
                    await Task.Delay(waitTimeSeconds * 1000);
                }
            }

            await _dbContext.SaveChangesAsync();
            if (processed > 0)
            {
                _logger.LogInformation("Preview generation job completed successfully. Processed {Count} items", processed);
            }
        }

        private async Task EnsureChunkExistsAsync(byte[] hash, long sizeBytes)
        {
            var existing = await _dbContext.Chunks.FindAsync([(object?)hash]);
            string storageKey = Hasher.ToHexStringHash(hash);
            long storedSizeBytes = await _storage.GetSizeAsync(storageKey);
            if (existing == null)
            {
                _dbContext.Chunks.Add(new Chunk
                {
                    Hash = hash,
                    PlainSizeBytes = sizeBytes,
                    StoredSizeBytes = storedSizeBytes,
                    CompressionAlgorithm = CompressionProcessor.Algorithm
                });
                return;
            }

            bool updated = false;
            if (existing.PlainSizeBytes <= 0)
            {
                existing.PlainSizeBytes = sizeBytes;
                updated = true;
            }

            if (existing.StoredSizeBytes <= 0)
            {
                existing.StoredSizeBytes = storedSizeBytes;
                updated = true;
            }

            if (updated)
            {
                _dbContext.Chunks.Update(existing);
            }
        }
    }
}
