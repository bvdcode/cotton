// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    /// <summary>
    /// Represents a ranked search hit projected by a layout search provider.
    /// </summary>
    public class LayoutSearchHit
    {
        /// <summary>
        /// Gets or sets the hit kind.
        /// </summary>
        public LayoutSearchHitKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the entity identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the node identifier used to resolve the hit path.
        /// </summary>
        public Guid NodeIdForPath { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized name key.
        /// </summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider score.
        /// </summary>
        public double Score { get; set; }
    }
}
