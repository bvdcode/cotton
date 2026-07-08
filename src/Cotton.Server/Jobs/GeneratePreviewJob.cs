// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
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
            IReadOnlyDictionary<string, int> generatorVersionsByContentType = PreviewGeneratorProvider.GetGeneratorVersionsByContentType();
            CancellationToken cancellationToken = context?.CancellationToken ?? CancellationToken.None;

            await NormalizeLegacyPreviewableContentTypesAsync(allSupportedMimeTypes, cancellationToken);

            HashSet<Guid> queuedOrProcessedItemIds = [];
            List<FileManifest> itemsToProcess = await LoadNextPreviewItemsAsync(
                allSupportedMimeTypes,
                generatorVersionsByContentType,
                MaxItemsPerRun,
                queuedOrProcessedItemIds,
                cancellationToken);

            LogPreviewQueueLoaded(itemsToProcess.Count);
            int processed = await ProcessPreviewQueueAsync(
                itemsToProcess,
                queuedOrProcessedItemIds,
                allSupportedMimeTypes,
                generatorVersionsByContentType,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            LogPreviewJobCompleted(processed);
        }

        private void LogPreviewQueueLoaded(int itemCount)
        {
            if (itemCount > 0)
            {
                _logger.LogInformation("Generating previews for {Count} file manifests", itemCount);
            }
        }

        private void LogPreviewJobCompleted(int processed)
        {
            if (processed > 0)
            {
                _logger.LogInformation("Preview generation job completed successfully. Processed {Count} items", processed);
            }
        }

        private async Task<int> ProcessPreviewQueueAsync(
            List<FileManifest> itemsToProcess,
            HashSet<Guid> queuedOrProcessedItemIds,
            IReadOnlyCollection<string> allSupportedMimeTypes,
            IReadOnlyDictionary<string, int> generatorVersionsByContentType,
            CancellationToken cancellationToken)
        {
            int processed = 0;
            int nextIndex = 0;
            while (nextIndex < itemsToProcess.Count && processed < MaxItemsPerRun)
            {
                FileManifest item = itemsToProcess[nextIndex++];
                processed++;

                try
                {
                    await ProcessPreviewItemAsync(item, processed, itemsToProcess.Count, cancellationToken);
                    await RefreshQueueAfterUploadPauseAsync(
                        itemsToProcess,
                        nextIndex,
                        queuedOrProcessedItemIds,
                        allSupportedMimeTypes,
                        generatorVersionsByContentType,
                        cancellationToken);
                }
                finally
                {
                    DetachPreviewItem(item);
                }
            }

            return processed;
        }

        private async Task ProcessPreviewItemAsync(
            FileManifest item,
            int processed,
            int total,
            CancellationToken cancellationToken)
        {
            _perf.OnPreviewGenerating();
            _logger.LogInformation("Processing {Current}/{Total}: FileManifest {FileManifestId}, ContentType={ContentType}, Size={Size}",
                processed, total, item.Id, item.ContentType, item.SizeBytes);

            IPreviewGenerator? generator = PreviewGeneratorProvider.GetGeneratorByContentType(item.ContentType);
            if (generator is null)
            {
                _logger.LogWarning("No preview generator found for content type {ContentType}", item.ContentType);
                return;
            }

            try
            {
                await GeneratePreviewAsync(item, generator, cancellationToken);
                await NotifyPreviewGeneratedAsync(item, cancellationToken);
                await ThrottlePreviewProcessingAsync(processed, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await RecordPreviewGenerationFailureAsync(item, generator, ex, cancellationToken);
            }
        }

        private async Task GeneratePreviewAsync(
            FileManifest item,
            IPreviewGenerator generator,
            CancellationToken cancellationToken)
        {
            PipelineContext pipelineContext = CreatePreviewPipelineContext(item);
            string[] uids = item.FileManifestChunks.GetChunkHashes();

            await StoreSmallPreviewAsync(item, generator, uids, pipelineContext, cancellationToken);
            await StoreLargePreviewIfSupportedAsync(item, generator, uids, pipelineContext, cancellationToken);

            item.PreviewGenerationError = null;
            item.PreviewGeneratorVersion = generator.Version;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Generated preview for file manifest {FileManifestId}", item.Id);
        }

        private static PipelineContext CreatePreviewPipelineContext(FileManifest item)
        {
            return new PipelineContext
            {
                FileSizeBytes = item.SizeBytes,
                ChunkLengths = item.FileManifestChunks.GetChunkLengths()
            };
        }

        private async Task StoreSmallPreviewAsync(
            FileManifest item,
            IPreviewGenerator generator,
            string[] uids,
            PipelineContext pipelineContext,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Getting blob stream for FileManifest {FileManifestId}...", item.Id);
            await using Stream source = _storage.GetBlobStream(uids, pipelineContext);
            byte[] previewImage = await generator.GeneratePreviewWebPAsync(source, PreviewGeneratorProvider.DefaultSmallPreviewSize);
            byte[] hash = await WritePreviewImageAsync(item.Id, "preview", previewImage, cancellationToken);
            item.SmallFilePreviewHash = hash;
            item.SmallFilePreviewHashEncrypted = _crypto.Encrypt(hash);
        }

        private async Task StoreLargePreviewIfSupportedAsync(
            FileManifest item,
            IPreviewGenerator generator,
            string[] uids,
            PipelineContext pipelineContext,
            CancellationToken cancellationToken)
        {
            if (!ShouldGenerateLargePreview(generator))
            {
                return;
            }

            await using Stream source = _storage.GetBlobStream(uids, pipelineContext);
            byte[] previewImage = await generator.GeneratePreviewWebPAsync(source, PreviewGeneratorProvider.DefaultLargePreviewSize);
            item.LargeFilePreviewHash = await WritePreviewImageAsync(item.Id, "large preview", previewImage, cancellationToken);
        }

        private static bool ShouldGenerateLargePreview(IPreviewGenerator generator) =>
            generator is ImagePreviewGenerator or HeicPreviewGenerator or SvgPreviewGenerator;

        private async Task<byte[]> WritePreviewImageAsync(
            Guid fileManifestId,
            string previewKind,
            byte[] previewImage,
            CancellationToken cancellationToken)
        {
            byte[] hash = Hasher.HashData(previewImage);
            string hashStr = Hasher.ToHexStringHash(hash);
            _logger.LogDebug("Storing {PreviewKind} (hash={Hash}) for FileManifest {FileManifestId}...",
                previewKind, hashStr, fileManifestId);

            using var resultStream = new MemoryStream(previewImage);
            await _storage.WriteAsync(hashStr, resultStream);
            await EnsureChunkExistsAsync(hash, previewImage.Length, cancellationToken);
            return hash;
        }

        private async Task NotifyPreviewGeneratedAsync(FileManifest item, CancellationToken cancellationToken)
        {
            foreach (NodeFile nodeFile in item.NodeFiles)
            {
                // Note: a regenerated preview hash can leak that another user already had this file even when cross-user dedup is disabled.
                await _hubContext.Clients
                    .User(nodeFile.OwnerId.ToString())
                    .SendAsync("PreviewGenerated", nodeFile.NodeId, nodeFile.Id, item.GetPreviewHashEncryptedHex(), cancellationToken);
            }
        }

        private async Task ThrottlePreviewProcessingAsync(int processed, CancellationToken cancellationToken)
        {
            if (processed == UnthrottledItemsCount)
            {
                _logger.LogInformation("Processed {Count} items, throttling further processing to avoid overloading the system...", UnthrottledItemsCount);
            }

            if (processed > UnthrottledItemsCount)
            {
                await Task.Delay(ThrottleDelayMs, cancellationToken);
            }
        }

        private async Task RecordPreviewGenerationFailureAsync(
            FileManifest item,
            IPreviewGenerator generator,
            Exception ex,
            CancellationToken cancellationToken)
        {
            _logger.LogWarning(ex, "Failed to generate preview for file manifest {FileManifestId}", item.Id);
            item.PreviewGenerationError = ex.Message;
            item.PreviewGeneratorVersion = generator.Version;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task RefreshQueueAfterUploadPauseAsync(
            List<FileManifest> itemsToProcess,
            int nextIndex,
            HashSet<Guid> queuedOrProcessedItemIds,
            IReadOnlyCollection<string> allSupportedMimeTypes,
            IReadOnlyDictionary<string, int> generatorVersionsByContentType,
            CancellationToken cancellationToken)
        {
            if (!_perf.IsUploading())
            {
                return;
            }

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

        private IQueryable<FileManifest> CreateItemsToProcessQuery(
            IReadOnlyCollection<string> allSupportedMimeTypes,
            IReadOnlyDictionary<string, int> generatorVersionsByContentType)
        {
            IQueryable<FileManifest> processableItemsQuery = _dbContext.FileManifests
                .Where(fm => allSupportedMimeTypes.Contains(fm.ContentType));

            IQueryable<FileManifest> itemsToProcessQuery = processableItemsQuery
                .Where(fm => fm.SmallFilePreviewHash == null || fm.SmallFilePreviewHashEncrypted == null)
                .Where(fm => fm.PreviewGenerationError == null);

            foreach (IGrouping<int, KeyValuePair<string, int>> versionGroup in generatorVersionsByContentType.GroupBy(x => x.Value))
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

            List<Guid> itemIds = await CreateItemsToProcessQuery(allSupportedMimeTypes, generatorVersionsByContentType)
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

            List<FileManifest> itemsToProcess = await _dbContext.FileManifests
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
            int remainingSlots = Math.Max(0, MaxItemsPerRun - insertIndex);
            if (remainingSlots == 0)
            {
                return 0;
            }

            List<FileManifest> refreshedItems = await LoadNextPreviewItemsAsync(
                allSupportedMimeTypes,
                generatorVersionsByContentType,
                Math.Min(RefreshItemsPerUploadPause, remainingSlots),
                queuedOrProcessedItemIds,
                cancellationToken);

            if (refreshedItems.Count == 0)
            {
                return 0;
            }

            itemsToProcess.InsertRange(insertIndex, refreshedItems);
            TrimPreviewQueueToRunLimit(itemsToProcess);
            return refreshedItems.Count;
        }

        private void TrimPreviewQueueToRunLimit(List<FileManifest> itemsToProcess)
        {
            if (itemsToProcess.Count <= MaxItemsPerRun)
            {
                return;
            }

            int removeStart = MaxItemsPerRun;
            int removeCount = itemsToProcess.Count - MaxItemsPerRun;
            for (int i = removeStart; i < itemsToProcess.Count; i++)
            {
                DetachPreviewItem(itemsToProcess[i]);
            }

            itemsToProcess.RemoveRange(removeStart, removeCount);
        }

        private void DetachPreviewItem(FileManifest item)
        {
            foreach (FileManifestChunk manifestChunk in item.FileManifestChunks)
            {
                if (manifestChunk.Chunk is not null)
                {
                    _dbContext.Entry(manifestChunk.Chunk).State = EntityState.Detached;
                }

                _dbContext.Entry(manifestChunk).State = EntityState.Detached;
            }

            foreach (NodeFile nodeFile in item.NodeFiles)
            {
                _dbContext.Entry(nodeFile).State = EntityState.Detached;
            }

            _dbContext.Entry(item).State = EntityState.Detached;
        }

        private async Task WaitForUploadPauseAsync(CancellationToken cancellationToken)
        {
            const int waitTimeSeconds = 5;
            _logger.LogInformation("Upload in progress, waiting {seconds}s before processing next item...", waitTimeSeconds);
            await Task.Delay(waitTimeSeconds * 1000, cancellationToken);
        }

        private async Task NormalizeLegacyPreviewableContentTypesAsync(
            IReadOnlyCollection<string> supportedContentTypes,
            CancellationToken cancellationToken)
        {
            List<FileManifest> manifests = await _dbContext.FileManifests
                .Include(m => m.NodeFiles)
                .Where(m =>
                    (m.ContentType == FileManifestService.DefaultContentType
                        || m.ContentType == string.Empty) &&
                    m.NodeFiles.Any(nf => Regex.IsMatch(
                        nf.Name,
                        FileManifestService.PreviewableFileNameRegexPattern,
                        RegexOptions.IgnoreCase)))
                .OrderBy(m => m.CreatedAt)
                .Take(MaxItemsPerRun)
                .ToListAsync(cancellationToken);

            int updated = 0;
            foreach (FileManifest? manifest in manifests)
            {
                string? fileName = manifest.NodeFiles.FirstOrDefault(nodeFile => Regex.IsMatch(
                    nodeFile.Name,
                    FileManifestService.PreviewableFileNameRegexPattern,
                    RegexOptions.IgnoreCase))?.Name;
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
                _logger.LogInformation("Normalized {Count} legacy file manifest content types before preview generation.", updated);
            }
        }

        private async Task EnsureChunkExistsAsync(byte[] hash, long sizeBytes, CancellationToken cancellationToken)
        {
            Chunk? existing = await _dbContext.Chunks.FindAsync(new object?[] { hash }, cancellationToken);
            string storageKey = Hasher.ToHexStringHash(hash);
            long storedSizeBytes = await _storage.GetSizeAsync(storageKey);
            if (existing is null)
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
