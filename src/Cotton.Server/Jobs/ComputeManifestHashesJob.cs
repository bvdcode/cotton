using Cotton.Database;
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
    [JobTrigger(hours: 1)]
    public class ComputeManifestHashesJob(
        PerfTracker _perf,
        IStoragePipeline _storage,
        INotificationsProvider _notifications,
        ILogger<ComputeManifestHashesJob> _logger,
        CottonDbContext _dbContext) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            if (_perf.IsUploading())
            {
                _logger.LogInformation("ComputeManifestHashesJob skipped: upload in progress.");
                return;
            }
            var unprocessedManifests = _dbContext.FileManifests
                .Include(fm => fm.FileManifestChunks)
                .Where(fm => fm.ComputedContentHash == null)
                .ToList();
            foreach (var manifest in unprocessedManifests)
            {
                await Task.Delay(250);

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
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Hash match for manifest {ManifestId}: {Hash}",
                        manifest.Id, Hasher.ToHexStringHash(manifest.ComputedContentHash));
                }
                else
                {
                    _logger.LogWarning("Hash mismatch for manifest {ManifestId}: computed {ComputedHash}, proposed {ProposedHash}",
                        manifest.Id,
                        Hasher.ToHexStringHash(computedContentHash),
                        Hasher.ToHexStringHash(manifest.ProposedContentHash));
                    var relatedFiles = await _dbContext.NodeFiles
                        .Where(nf => nf.FileManifestId == manifest.Id)
                        .ToListAsync();

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
