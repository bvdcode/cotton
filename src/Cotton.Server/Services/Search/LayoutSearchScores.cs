// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search;

/// <summary>
/// Defines normalized relevance scores shared by layout search providers.
/// </summary>
public static class LayoutSearchScores
{
    /// <summary>Exact identifier matches are certain.</summary>
    public const double ExactIdentifier = 1.0;

    /// <summary>Exact normalized name matches are very strong lexical hits.</summary>
    public const double ExactName = 0.8;

    /// <summary>Prefix lexical matches are strong hits.</summary>
    public const double PrefixName = 0.6;

    /// <summary>Multi-token lexical matches are medium hits.</summary>
    public const double TokenName = 0.4;

    /// <summary>Substring lexical matches are weak hits.</summary>
    public const double SubstringName = 0.2;
}
