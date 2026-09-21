// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Handlers.Files
{
    public record RecoverFileTextIndexRequest : IRequest;

    public class RecoverFileTextIndexRequestHandler(
        CottonDbContext dbContext, IMemoryCache cache, ILogger<RecoverFileTextIndexRequestHandler> logger)
        : IRequestHandler<RecoverFileTextIndexRequest>
    {
        private const int BatchSize = 500;

        public async Task Handle(RecoverFileTextIndexRequest request, CancellationToken cancellationToken)
        {
            if (cache.Get<bool>(typeof(RecoverFileTextIndexRequest)))
            {
                return;
            }
            if (!await dbContext.FileEmbeddings.AnyAsync(cancellationToken))
            {
                logger.LogInformation("Embedding table is empty. Resetting file text indexing state.");
                Guid? lastId = null;
                long resetCount = 0;
                while (true)
                {
                    var query = dbContext.FileManifests
                        .Where(manifest => manifest.TextIndexVersion != 0 || manifest.TextIndexError != null);
                    if (lastId.HasValue)
                    {
                        query = query.Where(manifest => manifest.Id.CompareTo(lastId.Value) > 0);
                    }
                    List<FileManifest> manifests = await query.OrderBy(manifest => manifest.Id)
                        .Take(BatchSize).ToListAsync(cancellationToken);
                    if (manifests.Count == 0)
                    {
                        break;
                    }
                    try
                    {
                        foreach (FileManifest manifest in manifests)
                        {
                            manifest.TextIndexVersion = 0;
                            manifest.TextIndexError = null;
                        }
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    finally
                    {
                        dbContext.ChangeTracker.Clear();
                    }
                    lastId = manifests[^1].Id;
                    resetCount += manifests.Count;
                    logger.LogInformation("Reset text indexing state for {ManifestCount} file manifests.", resetCount);
                }
                logger.LogInformation("File text index recovery completed. Reset {ManifestCount} file manifests.", resetCount);
            }
            cache.Set(typeof(RecoverFileTextIndexRequest), true,
                new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
        }
    }
}
