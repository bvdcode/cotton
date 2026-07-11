// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
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
    /// <summary>
    /// Runs the scheduled compute manifest hashes maintenance task.
    /// </summary>
    [JobTrigger(hours: 12)]
    public class ComputeManifestHashesJob(
        PerfTracker _perf,
        IStoragePipeline _storage,
        INotificationsProvider _notifications,
        ILogger<ComputeManifestHashesJob> _logger,
        CottonDbContext _dbContext) : IJob
    {
        private const int MaxItemsPerRun = 1000;
        private static readonly HashSet<Guid> KnownMismatchedManifestIds = [];

        /// <summary>
        /// Executes the scheduled Quartz job.
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            if (_perf.IsUploading())
            {
                _logger.LogInformation("ComputeManifestHashesJob skipped: upload in progress.");
                return;
            }
            if (_perf.IsNightTime())
            {
                _logger.LogInformation("ComputeManifestHashesJob skipped: night time.");
                return;
            }

            Guid[] knownMismatchedManifestIds = [.. KnownMismatchedManifestIds];
            IQueryable<FileManifest> unprocessedManifestsQuery = _dbContext.FileManifests
                .Where(fm => fm.ComputedContentHash == null);

            if (knownMismatchedManifestIds.Length > 0)
            {
                unprocessedManifestsQuery = unprocessedManifestsQuery
                    .Where(fm => !knownMismatchedManifestIds.Contains(fm.Id));
            }

            List<FileManifest> unprocessedManifests = await unprocessedManifestsQuery
                .Include(fm => fm.FileManifestChunks)
                .OrderBy(fm => fm.Id)
                .Take(MaxItemsPerRun)
                .ToListAsync(context.CancellationToken);

            foreach (FileManifest? manifest in unprocessedManifests)
            {
                if (_perf.IsPreviewGenerating() || _perf.IsUploading())
                {
                    await Task.Delay(60_000, context.CancellationToken);
                }
                else
                {
                    await Task.Delay(250, context.CancellationToken);
                }

                _logger.LogInformation("Computing hash for manifest {ManifestId}", manifest.Id);
                string[] hashes = manifest.FileManifestChunks.GetChunkHashes();
                PipelineContext pipelineContext = new()
                {
                    FileSizeBytes = manifest.SizeBytes
                };
                using Stream stream = _storage.GetBlobStream(hashes, pipelineContext);
                var computedContentHash = Hasher.HashData(stream);
                if (computedContentHash.SequenceEqual(manifest.ProposedContentHash))
                {
                    manifest.ComputedContentHash = computedContentHash;
                    await _dbContext.SaveChangesAsync(context.CancellationToken);
                    KnownMismatchedManifestIds.Remove(manifest.Id);
                    _logger.LogInformation("Hash match for manifest {ManifestId}: {Hash}",
                        manifest.Id, Hasher.ToHexStringHash(manifest.ComputedContentHash));
                }
                else
                {
                    _logger.LogWarning("Hash mismatch for manifest {ManifestId}: computed {ComputedHash}, proposed {ProposedHash}",
                        manifest.Id,
                        Hasher.ToHexStringHash(computedContentHash),
                        Hasher.ToHexStringHash(manifest.ProposedContentHash));

                    if (!KnownMismatchedManifestIds.Add(manifest.Id))
                    {
                        _logger.LogInformation(
                            "Hash mismatch for manifest {ManifestId} was already reported in this process.",
                            manifest.Id);
                        continue;
                    }

                    var relatedFiles = await _dbContext.NodeFiles
                        .AsNoTracking()
                        .Where(nf => nf.FileManifestId == manifest.Id)
                        .Select(nf => new
                        {
                            nf.OwnerId,
                            nf.Name,
                        })
                        .ToListAsync(context.CancellationToken);

                    // send notification for each related file
                    foreach (var file in relatedFiles)
                    {
                        await _notifications.SendUploadHashMismatchNotificationAsync(
                            file.OwnerId,
                            file.Name,
                            Hasher.ToHexStringHash(manifest.ProposedContentHash),
                            Hasher.ToHexStringHash(computedContentHash));
                    }
                }
            }
        }
    }
}
