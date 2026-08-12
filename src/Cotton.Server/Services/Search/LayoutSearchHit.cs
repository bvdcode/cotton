// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    public class LayoutSearchHit
    {
        public LayoutSearchHitKind Kind { get; set; }

        public Guid Id { get; set; }

        public Guid NodeIdForPath { get; set; }

        public string Name { get; set; } = string.Empty;

        public string NameKey { get; set; } = string.Empty;

        public double Score { get; set; }
    }
}
