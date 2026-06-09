// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync
{
    internal static class SyncRunProgressReporter
    {
        public static void Report(
            SyncRunOptions options,
            SyncRunProgressStage stage,
            int filesCompleted,
            int? filesTotal,
            string? currentPath,
            DateTime startedAtUtc,
            bool isCompleted = false,
            long bytesCompleted = 0,
            long? bytesTotal = null)
        {
            options.RunProgress?.Report(new SyncRunProgress(
                stage,
                filesCompleted,
                filesTotal,
                currentPath,
                startedAtUtc,
                isCompleted,
                bytesCompleted,
                bytesTotal));
        }
    }
}
