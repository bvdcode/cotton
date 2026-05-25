// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Extensions;
using Cotton.Server.Hubs;
using Cotton.Server.Providers;
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
using System.Text.RegularExpressions;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Runs the scheduled generate preview maintenance task.
    /// </summary>
    [JobTrigger(minutes: 15)]
    [DisallowConcurrentExecution]
    public class GeneratePreviewJob(
        PerfTracker _perf,
        IStreamCipher _crypto,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        IHubContext<EventHub> _hubContext,
        ILogger<GeneratePreviewJob> _logger) : IJob
    {
        private const int MaxItemsPerRun = 10000;
        private const int RefreshItemsPerUploadPause = 250;
        private const int UnthrottledItemsCount = 1000;
        private const int ThrottleDelayMs = 250;

        /// <summary>
        /// Executes the scheduled Quartz job.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            var allSupportedMimeTypes = PreviewGeneratorProvider.GetAllSupportedMimeTypes();
            var generatorVersionsByContentType = PreviewGeneratorProvider.GetGeneratorVersionsByContentType();
            CancellationToken cancellationToken = context?.CancellationToken ?? CancellationToken.None;

            await NormalizeLegacySourceTextContentTypesAsync(allSupportedMimeTypes, cancellationToken);

            HashSet<Guid> queuedOrProcessedItemIds = [];
            List<FileManifest> itemsToProcess = await LoadNextPreviewItemsAsync(
                allSupportedMimeTypes,
                generatorVersionsByContentType,
                MaxItemsPerRun,
                queuedOrProcessedItemIds,
                cancellationToken);

            if (itemsToProcess.Count > 0)
            {
                _logger.LogInformation("Generating previews for {Count} file manifests", itemsToProcess.Count);
            }

            int processed = 0;
            int nextIndex = 0;
            while (nextIndex < itemsToProcess.Count)
            {
                var item = itemsToProcess[nextIndex++];
                _perf.OnPreviewGenerating();
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
                    await EnsureChunkExistsAsync(hash, previewImage.Length, cancellationToken);
                    item.SmallFilePreviewHash = hash;
                    item.SmallFilePreviewHashEncrypted = _crypto.Encrypt(hash);
                    item.PreviewGenerationError = null;
                    item.PreviewGeneratorVersion = generator.Version;

                    if (generator is ImagePreviewGenerator or HeicPreviewGenerator or SvgPreviewGenerator)
                    {
                        await using var fsLarge = _storage.GetBlobStream(uids, pipelineContext);
                        byte[] previewImageLarge = await generator.GeneratePreviewWebPAsync(fsLarge, PreviewGeneratorProvider.DefaultLargePreviewSize);
                        byte[] hashLarge = Hasher.HashData(previewImageLarge);
                        string hashLargeStr = Hasher.ToHexStringHash(hashLarge);
                        _logger.LogDebug("Storing large preview (hash={Hash}) for FileManifest {FileManifestId}...", hashLargeStr, item.Id);
                        using var resultStreamLarge = new MemoryStream(previewImageLarge);
                        await _storage.WriteAsync(hashLargeStr, resultStreamLarge);
                        await EnsureChunkExistsAsync(hashLarge, previewImageLarge.Length, cancellationToken);
                        item.LargeFilePreviewHash = hashLarge;
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogDebug("Generated preview for file manifest {FileManifestId}", item.Id);
                    foreach (var nodeFile in item.NodeFiles)
                    {
                        // Minor vulnerability:
                        // Even if cross-user deduplication is disabled, this event could reveal to a user who already had the file that someone else had the file,
                        // because the preview hash will be reset and regenerated, preventing the second user from discovering that the first user had the file.
                        await _hubContext.Clients
                            .User(nodeFile.OwnerId.ToString())
                            .SendAsync("PreviewGenerated", nodeFile.NodeId, nodeFile.Id, item.GetPreviewHashEncryptedHex(), cancellationToken);
                    }
                    if (processed == UnthrottledItemsCount)
                    {
                        _logger.LogInformation("Processed {Count} items, throttling further processing to avoid overloading the system...", UnthrottledItemsCount);
                    }
                    if (processed > UnthrottledItemsCount)
                    {
                        // TODO: Move to settings or autoconfig
                        await Task.Delay(ThrottleDelayMs, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate preview for file manifest {FileManifestId}", item.Id);
                    item.PreviewGenerationError = ex.Message;
                    item.PreviewGeneratorVersion = generator.Version;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                if (_perf.IsUploading())
                {
                    await WaitForUploadPauseAsync(cancellationToken);
                    int refreshed = await RefreshPreviewQueueAsync(
                        itemsToProcess,
                        nextIndex,
                        queuedOrProcessedItemIds,
                        allSupportedMimeTypes,
                        generatorVersionsByContentType,
                        cancellationToken);
                    if (refreshed > 0)
                    {
                        _logger.LogInformation(
                            "Upload pause refreshed preview queue with {Count} newer file manifests. Queue now has {Total} items.",
                            refreshed,
                            itemsToProcess.Count);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            if (processed > 0)
            {
                _logger.LogInformation("Preview generation job completed successfully. Processed {Count} items", processed);
            }
        }

        private IQueryable<FileManifest> CreateItemsToProcessQuery(
            IReadOnlyCollection<string> allSupportedMimeTypes,
            IReadOnlyDictionary<string, int> generatorVersionsByContentType)
        {
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

            return itemsToProcessQuery;
        }

        private async Task<List<FileManifest>> LoadNextPreviewItemsAsync(
            IReadOnlyCollection<string> allSupportedMimeTypes,
            IReadOnlyDictionary<string, int> generatorVersionsByContentType,
            int limit,
            HashSet<Guid> knownItemIds,
            CancellationToken cancellationToken)
        {
            if (limit <= 0)
            {
                return [];
            }

            var itemIds = await CreateItemsToProcessQuery(allSupportedMimeTypes, generatorVersionsByContentType)
                .OrderByDescending(fm => fm.CreatedAt)
                .Select(fm => fm.Id)
                .Take(limit)
                .ToListAsync(cancellationToken);

            List<Guid> newItemIds = [.. itemIds.Where(id => knownItemIds.Add(id))];
            return await LoadPreviewItemsByIdsAsync(newItemIds, cancellationToken);
        }

        private async Task<List<FileManifest>> LoadPreviewItemsByIdsAsync(
            IReadOnlyCollection<Guid> itemIds,
            CancellationToken cancellationToken)
        {
            if (itemIds.Count == 0)
            {
                return [];
            }

            var itemsToProcess = await _dbContext.FileManifests
                .Where(fm => itemIds.Contains(fm.Id))
                .Include(fm => fm.NodeFiles)
                .Include(fm => fm.FileManifestChunks)
                .ThenInclude(fmc => fmc.Chunk)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var itemsToProcessById = itemsToProcess.ToDictionary(x => x.Id);
            return [.. itemIds.Where(itemsToProcessById.ContainsKey).Select(id => itemsToProcessById[id])];
        }

        private async Task<int> RefreshPreviewQueueAsync(
            List<FileManifest> itemsToProcess,
            int insertIndex,
            HashSet<Guid> queuedOrProcessedItemIds,
            IReadOnlyCollection<string> allSupportedMimeTypes,
            IReadOnlyDictionary<string, int> generatorVersionsByContentType,
            CancellationToken cancellationToken)
        {
            List<FileManifest> refreshedItems = await LoadNextPreviewItemsAsync(
                allSupportedMimeTypes,
                generatorVersionsByContentType,
                RefreshItemsPerUploadPause,
                queuedOrProcessedItemIds,
                cancellationToken);

            if (refreshedItems.Count == 0)
            {
                return 0;
            }

            itemsToProcess.InsertRange(insertIndex, refreshedItems);
            return refreshedItems.Count;
        }

        private async Task WaitForUploadPauseAsync(CancellationToken cancellationToken)
        {
            const int waitTimeSeconds = 5;
            _logger.LogInformation("Upload in progress, waiting {seconds}s before processing next item...", waitTimeSeconds);
            await Task.Delay(waitTimeSeconds * 1000, cancellationToken);
        }

        private async Task NormalizeLegacySourceTextContentTypesAsync(
            IReadOnlyCollection<string> supportedContentTypes,
            CancellationToken cancellationToken)
        {
            var manifests = await _dbContext.FileManifests
                .Include(m => m.NodeFiles)
                .Where(m =>
                    (m.ContentType == FileManifestService.DefaultContentType
                        || m.ContentType == string.Empty) &&
                    m.NodeFiles.Any(nf => Regex.IsMatch(
                        nf.Name,
                        FileManifestService.SourceTextFileNameRegexPattern,
                        RegexOptions.IgnoreCase)))
                .OrderBy(m => m.CreatedAt)
                .Take(MaxItemsPerRun)
                .ToListAsync(cancellationToken);

            int updated = 0;
            foreach (var manifest in manifests)
            {
                string? fileName = manifest.NodeFiles.FirstOrDefault(nodeFile => FileManifestService.IsSourceTextFileName(nodeFile.Name))?.Name;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                string contentType = FileManifestService.ResolveContentType(fileName, manifest.ContentType);
                if (!supportedContentTypes.Contains(contentType))
                {
                    continue;
                }

                manifest.ContentType = contentType;
                updated++;
            }

            if (updated > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Normalized {Count} legacy source-text file manifest content types before preview generation.", updated);
            }
        }

        private async Task EnsureChunkExistsAsync(byte[] hash, long sizeBytes, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.Chunks.FindAsync(new object?[] { hash }, cancellationToken);
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
            if (existing.GCScheduledAfter.HasValue)
            {
                existing.GCScheduledAfter = null;
                updated = true;
            }

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
