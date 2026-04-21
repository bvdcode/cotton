using Cotton.Database;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class BackfillChunkStoredSizeJob(
        CottonDbContext _dbContext,
        IStoragePipeline _storage,
        ILogger<BackfillChunkStoredSizeJob> _logger) : IJob
    {
        private const int BatchSize = 1000;

        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken ct = context.CancellationToken;
            _logger.LogInformation("Backfill chunk stored size job started.");

            int totalProcessed = 0;
            int totalUpdated = 0;

            while (true)
            {
                var batch = await _dbContext.Chunks
                    .Where(c => c.StoredSizeBytes <= 0)
                    .OrderBy(c => c.Hash)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var chunk in batch)
                {
                    ct.ThrowIfCancellationRequested();

                    string uid = Hasher.ToHexStringHash(chunk.Hash);
                    long storedSizeBytes = await _storage.GetSizeAsync(uid);

                    if (storedSizeBytes > 0 || uid == Hasher.ZeroHashHexString)
                    {
                        chunk.StoredSizeBytes = storedSizeBytes;
                        totalUpdated++;
                    }

                    totalProcessed++;
                }

                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Backfill chunk stored size progress: processed {Processed}, updated {Updated}.",
                    totalProcessed,
                    totalUpdated);
            }

            _logger.LogInformation(
                "Backfill chunk stored size job finished. Processed {Processed}, updated {Updated}.",
                totalProcessed,
                totalUpdated);
        }
    }
}
