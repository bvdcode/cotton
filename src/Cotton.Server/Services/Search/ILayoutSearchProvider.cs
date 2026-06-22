// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    /// <summary>
    /// Defines a provider that contributes ranked hits to layout search.
    /// </summary>
    public interface ILayoutSearchProvider
    {
        /// <summary>Gets provider ordering for deterministic query composition.</summary>
        int Priority { get; }

        /// <summary>Returns whether the provider can serve the supplied criteria.</summary>
        bool CanSearch(LayoutSearchCriteria criteria);

        /// <summary>Builds the provider hit query.</summary>
        IQueryable<LayoutSearchHit> BuildHitsQuery(LayoutSearchProviderContext context);
    }
}
