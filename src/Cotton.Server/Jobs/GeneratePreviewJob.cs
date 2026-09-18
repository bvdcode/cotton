// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Hubs;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.Previews;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Processors;
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
        FilePreviewRenderer _renderer,
        CottonDbContext _dbContext,
        IHubContext<EventHub> _hubContext,
        ILogger<GeneratePreviewJob> _logger) : IJob
    {
        private const int MaxItemsPerRun = 10000;
        private const int RefreshItemsPerUploadPause = 250;
        private const int UnthrottledItemsCount = 1000;
        private const int ThrottleDelayMs = 250;

        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context?.CancellationToken ?? CancellationToken.None;

            HashSet<Guid> queuedOrProcessedItemIds = [];
            List<FileManifest> itemsToProcess = await PreviewQueueLoader.LoadNextAsync(
                _dbContext,
                MaxItemsPerRun,
                queuedOrProcessedItemIds,
                cancellationToken);

            LogPreviewQueueLoaded(itemsToProcess.Count);
            int processed = await ProcessPreviewQueueAsync(
                itemsToProcess,
                queuedOrProcessedItemIds,
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
            _logger.LogInformation("Processing {Current}/{Total}: FileManifest {FileManifestId}, Size={Size}",
                processed, total, item.Id, item.SizeBytes);

            try
            {
                RenderedFilePreview? preview = await _renderer.RenderAsync(item, cancellationToken);
                if (preview is null)
                {
                    await RecordPreviewGenerationFailureAsync(item, "No preview generator matches the file names.", cancellationToken);
                    return;
                }

                await StorePreviewAsync(item, preview, cancellationToken);
                await NotifyPreviewGeneratedAsync(item, cancellationToken);
                await ThrottlePreviewProcessingAsync(processed, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogInformation(
                    ex,
                    "Skipped stale preview update for file manifest {FileManifestId}.",
                    item.Id);
            }
            catch (AggregateException ex)
            {
                _logger.LogWarning(ex, "Failed to render preview for file manifest {FileManifestId}", item.Id);
                await RecordPreviewGenerationFailureAsync(item, ex.Message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store or notify preview for file manifest {FileManifestId}", item.Id);
            }
        }

        private async Task StorePreviewAsync(
            FileManifest item,
            RenderedFilePreview preview,
            CancellationToken cancellationToken)
        {
            byte[] smallHash = await WritePreviewImageAsync(item.Id, "preview", preview.Small, cancellationToken);
            byte[] encryptedHash = await _crypto.EncryptAsync(smallHash, cancellationToken: cancellationToken);
            byte[]? largeHash = null;
            if (preview.Large is not null)
            {
                largeHash = await WritePreviewImageAsync(item.Id, "large preview", preview.Large, cancellationToken);
            }

            item.SmallFilePreviewHash = smallHash;
            item.SmallFilePreviewHashEncrypted = encryptedHash;
            item.LargeFilePreviewHash = largeHash;
            item.PreviewGenerationError = null;
            item.PreviewGeneratorVersion = PreviewGeneratorProvider.GenerationVersion;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Generated preview for file manifest {FileManifestId}", item.Id);
        }

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

            using MemoryStream resultStream = new MemoryStream(previewImage);
            await _storage.WriteAsync(
                hashStr,
                resultStream,
                cancellationToken: cancellationToken);
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
            string error,
            CancellationToken cancellationToken)
        {
            HashSet<string> attemptedNames = item.NodeFiles.Select(file => file.Name).ToHashSet(StringComparer.Ordinal);
            string[] currentNames = await PreviewFileQuery.AvailableFiles(_dbContext)
                .Where(file => file.FileManifestId == item.Id).Select(file => file.Name).ToArrayAsync(cancellationToken);
            if (!attemptedNames.SetEquals(currentNames))
            {
                return;
            }

            item.PreviewGenerationError = error;
            item.PreviewGeneratorVersion = PreviewGeneratorProvider.GenerationVersion;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException conflict)
            {
                _logger.LogInformation(
                    conflict,
                    "Skipped stale preview failure update for file manifest {FileManifestId}.",
                    item.Id);
            }
        }

        private async Task RefreshQueueAfterUploadPauseAsync(
            List<FileManifest> itemsToProcess,
            int nextIndex,
            HashSet<Guid> queuedOrProcessedItemIds,
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
                cancellationToken);

            if (refreshed > 0)
            {
                _logger.LogInformation(
                    "Upload pause refreshed preview queue with {Count} newer file manifests. Queue now has {Total} items.",
                    refreshed,
                    itemsToProcess.Count);
            }
        }

        private async Task<int> RefreshPreviewQueueAsync(
            List<FileManifest> itemsToProcess,
            int insertIndex,
            HashSet<Guid> queuedOrProcessedItemIds,
            CancellationToken cancellationToken)
        {
            int remainingSlots = Math.Max(0, MaxItemsPerRun - insertIndex);
            if (remainingSlots == 0)
            {
                return 0;
            }

            List<FileManifest> refreshedItems = await PreviewQueueLoader.LoadNextAsync(
                _dbContext,
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
