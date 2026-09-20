// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Cotton.Server.Handlers.Files
{
    public record ClearFileManifestContentTypesRequest : IRequest<int>;

    public class ClearFileManifestContentTypesRequestHandler(
        CottonDbContext dbContext,
        ILogger<ClearFileManifestContentTypesRequestHandler> logger)
        : IRequestHandler<ClearFileManifestContentTypesRequest, int>
    {
        private const int BatchSize = 5000;

        public async Task<int> Handle(ClearFileManifestContentTypesRequest request, CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            logger.LogInformation("Clearing obsolete file manifest content types and upgrading signatures. Batch size: {BatchSize}", BatchSize);
            Guid? afterId = null;
            int updated = 0;
            while (true)
            {
                IQueryable<FileManifest> query = dbContext.FileManifests.Where(manifest =>
                    manifest.ContentType != string.Empty
                    || EF.Property<int?>(manifest, DatabaseIntegrityColumns.VersionProperty) != FileManifestIntegrityDescriptor.LatestVersion);
                if (afterId is Guid lastId)
                {
                    query = query.Where(manifest => manifest.Id.CompareTo(lastId) > 0);
                }

                List<FileManifest> manifests = await query.OrderBy(manifest => manifest.Id)
                    .Take(BatchSize).ToListAsync(cancellationToken);
                if (manifests.Count == 0)
                {
                    logger.LogInformation(
                        "File manifest preparation completed. Updated {Count} manifests in {ElapsedSeconds:F1}s",
                        updated, stopwatch.Elapsed.TotalSeconds);
                    return updated;
                }

                foreach (FileManifest manifest in manifests)
                {
                    manifest.ContentType = string.Empty;
                    dbContext.Entry(manifest).Property(entity => entity.ContentType).IsModified = true;
                }
                await dbContext.SaveChangesAsync(cancellationToken);
                updated += manifests.Count;
                afterId = manifests[^1].Id;
                foreach (FileManifest manifest in manifests)
                {
                    dbContext.Entry(manifest).State = EntityState.Detached;
                }
                logger.LogInformation(
                    "File manifest preparation batch completed. Updated {BatchCount} manifests, {TotalCount} total in {ElapsedSeconds:F1}s",
                    manifests.Count, updated, stopwatch.Elapsed.TotalSeconds);
            }
        }
    }
}
