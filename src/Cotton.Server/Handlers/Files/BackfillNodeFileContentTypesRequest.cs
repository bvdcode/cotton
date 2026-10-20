// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Cotton.Server.Handlers.Files
{
    public record BackfillNodeFileContentTypesRequest : IRequest<int>;

    public class BackfillNodeFileContentTypesRequestHandler(
        CottonDbContext dbContext,
        IDatabaseIntegrityDescriptorRegistry descriptors,
        ILogger<BackfillNodeFileContentTypesRequestHandler> logger)
        : IRequestHandler<BackfillNodeFileContentTypesRequest, int>
    {
        private const int BatchSize = 500;

        public async Task<int> Handle(BackfillNodeFileContentTypesRequest request, CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            logger.LogInformation("Preparing node file content types and signatures. Batch size: {BatchSize}", BatchSize);
            Guid? afterId = null;
            int updated = 0;
            IDatabaseIntegrityDescriptor<NodeFile> descriptor = descriptors.Get<NodeFile>();
            while (true)
            {
                IQueryable<NodeFile> query = dbContext.NodeFiles.Where(file => file.ContentType == string.Empty
                    || EF.Property<int?>(file, DatabaseIntegrityColumns.VersionProperty) != descriptor.SchemaVersion);
                if (afterId is Guid lastId)
                {
                    query = query.Where(file => file.Id.CompareTo(lastId) > 0);
                }

                List<NodeFile> files = await query.OrderBy(file => file.Id)
                    .Take(BatchSize).ToListAsync(cancellationToken);
                if (files.Count == 0)
                {
                    logger.LogInformation(
                        "Node file preparation completed. Updated {Count} files in {ElapsedSeconds:F1}s",
                        updated, stopwatch.Elapsed.TotalSeconds);
                    return updated;
                }

                foreach (NodeFile file in files)
                {
                    if (file.ContentType == string.Empty)
                    {
                        dbContext.Entry(file).Property(entity => entity.ContentType).CurrentValue =
                            FileContentTypeResolver.ResolveFromFileName(file.Name);
                    }
                    dbContext.Entry(file).Property(entity => entity.ContentType).IsModified = true;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                updated += files.Count;
                afterId = files[^1].Id;
                foreach (NodeFile file in files)
                {
                    dbContext.Entry(file).State = EntityState.Detached;
                }
                logger.LogInformation(
                    "Node file preparation batch completed. Updated {BatchCount} files, {TotalCount} total in {ElapsedSeconds:F1}s",
                    files.Count, updated, stopwatch.Elapsed.TotalSeconds);
            }
        }
    }
}
