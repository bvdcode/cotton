// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search;

/// <summary>
/// Represents one normalized text token and its search patterns.
/// </summary>
public sealed record LayoutSearchToken(
    string NameKey,
    string ContainsPattern,
    string PrefixPattern,
    bool HasLetters);
