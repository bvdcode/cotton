// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Files
{
    public record BackfillNodeFileContentTypesRequest : IRequest<int>;

    public class BackfillNodeFileContentTypesRequestHandler(CottonDbContext dbContext)
        : IRequestHandler<BackfillNodeFileContentTypesRequest, int>
    {
        private const int BatchSize = 1000;

        public async Task<int> Handle(BackfillNodeFileContentTypesRequest request, CancellationToken cancellationToken)
        {
            Guid? afterId = null;
            int updated = 0;
            while (true)
            {
                IQueryable<NodeFile> query = dbContext.NodeFiles.Where(file => file.ContentType == string.Empty);
                if (afterId is Guid lastId)
                {
                    query = query.Where(file => file.Id.CompareTo(lastId) > 0);
                }

                var files = await query.OrderBy(file => file.Id)
                    .Select(file => new { file.Id, file.Name })
                    .Take(BatchSize).ToListAsync(cancellationToken);
                if (files.Count == 0)
                {
                    return updated;
                }

                foreach (IGrouping<string, Guid> group in files.GroupBy(
                    file => FileContentTypeResolver.ResolveFromFileName(file.Name), file => file.Id))
                {
                    Guid[] ids = [.. group];
                    string contentType = group.Key;
                    updated += await dbContext.NodeFiles
                        .Where(file => ids.Contains(file.Id) && file.ContentType == string.Empty)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.ContentType, contentType), cancellationToken);
                }

                afterId = files[^1].Id;
            }
        }
    }
}
