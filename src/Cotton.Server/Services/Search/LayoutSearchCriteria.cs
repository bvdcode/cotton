// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    public record LayoutSearchCriteria(
        string NameKey,
        string ContainsPattern,
        string PrefixPattern,
        string LikeEscape,
        LayoutSearchToken[] TextTokens,
        Guid[] IdQueries)
    {
        public bool HasText => NameKey.Length > 0;

        public bool HasIds => IdQueries.Length > 0;

        public bool HasOnlyIds => HasIds && !HasText;

        public bool HasVectorSearchText => TextTokens.Any(x => x.HasLetters);

        public bool HasMultipleTextTokens => TextTokens.Length > 1;
    }
}
