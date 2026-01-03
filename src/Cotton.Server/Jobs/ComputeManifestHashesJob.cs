using Cotton.Database;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(hours: 1)]
    public class ComputeManifestHashesJob(
        IStoragePipeline _storage,
        ILogger<ComputeManifestHashesJob> _logger,
        CottonDbContext _dbContext) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var unprocessedManifests = _dbContext.FileManifests
                .Include(fm => fm.FileManifestChunks)
                .Where(fm => fm.ComputedContentHash == null)
                .ToList();
            foreach (var manifest in unprocessedManifests)
            {
                string[] hashes = [.. manifest.FileManifestChunks
                .OrderBy(x => x.ChunkOrder)
                .Select(x => Hasher.ToHexStringHash(x.ChunkHash))];
                using Stream stream = _storage.GetBlobStream(hashes);
                var computedContentHash = Hasher.HashData(stream);
                if (!computedContentHash.SequenceEqual(manifest.ProposedContentHash))
                {
                    _logger.LogWarning("Hash mismatch for manifest {ManifestId}: computed {ComputedHash}, proposed {ProposedHash}",
                        manifest.Id,
                        Hasher.ToHexStringHash(computedContentHash),
                        Hasher.ToHexStringHash(manifest.ProposedContentHash));
                }
                else
                {
                    manifest.ComputedContentHash = computedContentHash;
                    _logger.LogInformation("Hash match for manifest {ManifestId}: {Hash}",
                        manifest.Id, Hasher.ToHexStringHash(manifest.ComputedContentHash));
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
