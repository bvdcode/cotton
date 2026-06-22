// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    /// <summary>
    /// Represents normalized query data shared by layout search providers.
    /// </summary>
    public record LayoutSearchCriteria(
        string NameKey,
        string ContainsPattern,
        string PrefixPattern,
        string LikeEscape,
        LayoutSearchToken[] TextTokens,
        Guid[] IdQueries)
    {
        /// <summary>Indicates whether the query contains searchable text.</summary>
        public bool HasText => NameKey.Length > 0;

        /// <summary>Indicates whether the query contains one or more identifiers.</summary>
        public bool HasIds => IdQueries.Length > 0;

        /// <summary>Indicates whether the query contains identifiers and no normalized text.</summary>
        public bool HasOnlyIds => HasIds && !HasText;

        /// <summary>Indicates whether the query contains natural-language text suitable for vector search.</summary>
        public bool HasVectorSearchText => TextTokens.Any(x => x.HasLetters);

        /// <summary>Indicates whether text search should match separate terms.</summary>
        public bool HasMultipleTextTokens => TextTokens.Length > 1;
    }
}
