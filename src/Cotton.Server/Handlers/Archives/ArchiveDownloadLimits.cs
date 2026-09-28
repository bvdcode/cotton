// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Archives
{
    internal static class ArchiveDownloadLimits
    {
        public const int BatchSize = 256;
        public const int PublicShareMaxEntries = 5_000;
        public const string PublicShareLimitMessage = "Shared folder archive is limited to 5000 entries.";
    }
}
