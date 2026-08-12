// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    public record LayoutSearchToken(
        string NameKey,
        string ContainsPattern,
        bool HasLetters);
}
