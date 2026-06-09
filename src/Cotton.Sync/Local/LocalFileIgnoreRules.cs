// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync.Local
{
    /// <summary>
    /// Identifies local files that should not enter the synchronization model.
    /// </summary>
    public static class LocalFileIgnoreRules
    {
        /// <summary>
        /// Returns whether the relative path should be skipped by local scanning.
        /// </summary>
        public static bool ShouldIgnore(string relativePath) => SyncPathIgnoreRules.ShouldIgnore(relativePath);
    }
}
