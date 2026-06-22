// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    /// <summary>
    /// Merges hits returned by independent layout search providers.
    /// </summary>
    public static class LayoutSearchHitMerger
    {
        /// <summary>
        /// Keeps the strongest hit for each returned entity before count and paging are applied.
        /// </summary>
        public static IQueryable<LayoutSearchHit> MergeDuplicateHits(IQueryable<LayoutSearchHit> hits)
        {
            return hits
                .GroupBy(x => new
                {
                    x.Kind,
                    x.Id,
                    x.NodeIdForPath,
                    x.Name,
                    x.NameKey,
                })
                .Select(x => new LayoutSearchHit
                {
                    Kind = x.Key.Kind,
                    Id = x.Key.Id,
                    NodeIdForPath = x.Key.NodeIdForPath,
                    Name = x.Key.Name,
                    NameKey = x.Key.NameKey,
                    Score = x.Max(hit => hit.Score),
                });
        }

        /// <summary>
        /// Keeps the strongest hit for each returned entity in already materialized hit sets.
        /// </summary>
        public static IReadOnlyList<LayoutSearchHit> MergeDuplicateHits(IEnumerable<LayoutSearchHit> hits)
        {
            return hits
                .GroupBy(x => new { x.Kind, x.Id })
                .Select(x => x
                    .OrderByDescending(hit => hit.Score)
                    .ThenBy(hit => hit.NameKey, StringComparer.Ordinal)
                    .ThenBy(hit => hit.NodeIdForPath)
                    .First())
                .ToList();
        }
    }
}
