// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Archives
{
    internal static class ArchiveDownloadLimits
    {
        public const int BatchSize = 256;
        public const int PublicShareMaxEntries = 100_000;
        public const string PublicShareLimitMessage = "This folder contains too many files and folders for a single archive. Please download the subfolders separately.";
    }
}
