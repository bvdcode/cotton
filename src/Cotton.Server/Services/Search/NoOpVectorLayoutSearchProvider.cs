// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.Search
{
    public class NoOpVectorLayoutSearchProvider(CottonDbContext _dbContext) : ILayoutSearchProvider
    {
        public int Priority => 100;

        public bool CanSearch(LayoutSearchCriteria criteria)
        {
            return criteria.HasVectorSearchText;
        }

        public IQueryable<LayoutSearchHit> BuildHitsQuery(LayoutSearchProviderContext context)
        {
            return _dbContext.NodeFiles
                .AsNoTracking()
                .Where(_ => false)
                .Select(x => new LayoutSearchHit
                {
                    Kind = LayoutSearchHitKind.File,
                    Id = x.Id,
                    NodeIdForPath = x.NodeId,
                    Name = x.Name,
                    NameKey = x.NameKey,
                    Score = 0,
                });
        }
    }
}
