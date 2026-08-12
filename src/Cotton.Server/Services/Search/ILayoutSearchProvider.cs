// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    public interface ILayoutSearchProvider
    {
        /// <summary>
        /// Gets provider ordering for deterministic query composition.
        /// </summary>
        int Priority { get; }

        bool CanSearch(LayoutSearchCriteria criteria);

        IQueryable<LayoutSearchHit> BuildHitsQuery(LayoutSearchProviderContext context);
    }
}
