// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Services;
using Cotton.Server.Services.FileMetadata;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Quartz.Attributes;
using EasyExtensions.Mediator;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Net;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Extracts deterministic content metadata for file manifests.
    /// </summary>
    [JobTrigger(minutes: 15)]
    public class ExtractFileMetadataJob(
        PerfTracker _perf,
        CottonDbContext _dbContext,
        IMediator _mediator,
        ILogger<ExtractFileMetadataJob> _logger) : IJob
    {
        private const int MaxItemsPerRun = 500;
        private const int UnthrottledItemsCount = 100;
        private const int UploadPauseDelayMs = 5000;
        private const int ThrottleDelayMs = 100;
        private static readonly string[] ImageContentTypes = [.. ImageFileContentMetadataExtractor.SupportedContentTypes];

        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context?.CancellationToken ?? CancellationToken.None;
            if (_perf.IsUploading())
            {
                await Task.Delay(UploadPauseDelayMs, cancellationToken);
            }

            List<Guid> manifestIds = await LoadNextItemIdsAsync(cancellationToken);
            if (manifestIds.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Extracting file metadata for {Count} file manifests.", manifestIds.Count);

            int processed = 0;
            foreach (Guid manifestId in manifestIds)
            {
                processed++;
                try
                {
                    await _mediator.Send(new ExtractFileManifestMetadataRequest
                    {
                        FileManifestId = manifestId,
                        Notify = true,
                    }, cancellationToken);
                    await ThrottleAsync(processed, cancellationToken);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogInformation(
                        ex,
                        "Skipped stale metadata update for file manifest {FileManifestId}.",
                        manifestId);
                }
                catch (WebApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    _logger.LogInformation(
                        ex,
                        "Skipped stale metadata update for file manifest {FileManifestId}.",
                        manifestId);
                }
                finally
                {
                    _dbContext.ChangeTracker.Clear();
                }
            }

            _logger.LogInformation("File metadata extraction job completed. Processed {Count} items.", processed);
        }

        private async Task<List<Guid>> LoadNextItemIdsAsync(CancellationToken cancellationToken)
        {
            return await CreateItemsToProcessQuery()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.Id)
                .Take(MaxItemsPerRun)
                .ToListAsync(cancellationToken);
        }

        private IQueryable<FileManifest> CreateItemsToProcessQuery()
        {
            return _dbContext.FileManifests
                .Where(x => x.NodeFiles.Any())
                .Where(x => x.Metadata == null)
                .Where(x =>
                    x.ContentType.StartsWith("audio/")
                    || x.ContentType.StartsWith("video/")
                    || ImageContentTypes.Contains(x.ContentType));
        }

        private async Task ThrottleAsync(int processed, CancellationToken cancellationToken)
        {
            if (processed <= UnthrottledItemsCount)
            {
                return;
            }

            await Task.Delay(ThrottleDelayMs, cancellationToken);
        }
    }
}
