// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    public static class LayoutSearchScores
    {
        public const double ExactIdentifier = 1.0;

        public const double ExactName = 0.8;

        public const double PrefixName = 0.6;

        public const double TokenName = 0.4;

        public const double SubstringName = 0.2;
    }
}
