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

namespace Cotton.Server.Handlers.Files
{
    public record BackfillNodeFileContentTypesRequest : IRequest<int>;

    public class BackfillNodeFileContentTypesRequestHandler(
        CottonDbContext dbContext,
        IDatabaseIntegrityVerifier verifier,
        IDatabaseIntegrityProtector protector,
        IDatabaseIntegrityDescriptorRegistry descriptors)
        : IRequestHandler<BackfillNodeFileContentTypesRequest, int>
    {
        private const int BatchSize = 500;

        public async Task<int> Handle(BackfillNodeFileContentTypesRequest request, CancellationToken cancellationToken)
        {
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
                    return updated;
                }

                foreach (NodeFile file in files)
                {
                    verifier.RequireValid(dbContext, file, "content-type.backfill");
                    byte[] originalMac = dbContext.Entry(file)
                        .Property<byte[]?>(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
                    string originalContentType = file.ContentType;
                    string contentType = originalContentType == string.Empty
                        ? FileContentTypeResolver.ResolveFromFileName(file.Name)
                        : originalContentType;
                    dbContext.Entry(file).Property(entity => entity.ContentType).CurrentValue = contentType;
                    byte[] mac = protector.Sign(file, descriptor);
                    int affected = await dbContext.NodeFiles
                        .Where(entity => entity.Id == file.Id && entity.ContentType == originalContentType
                            && EF.Property<byte[]?>(entity, DatabaseIntegrityColumns.MacProperty) == originalMac)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(entity => entity.ContentType, contentType)
                            .SetProperty(entity => EF.Property<int?>(entity, DatabaseIntegrityColumns.VersionProperty), descriptor.SchemaVersion)
                            .SetProperty(entity => EF.Property<byte[]?>(entity, DatabaseIntegrityColumns.MacProperty), mac), cancellationToken);
                    if (affected != 1)
                    {
                        throw new DbUpdateConcurrencyException("The node file changed while its content type was being backfilled.");
                    }
                    updated += affected;
                    dbContext.Entry(file).State = EntityState.Detached;
                }

                afterId = files[^1].Id;
            }
        }
    }
}
