// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Local;

namespace Cotton.Sync.App.LocalChanges
{

    /// <summary>
    /// Applies the sync scanner ignore policy to filesystem watcher events.
    /// </summary>
    public sealed class LocalSyncRootChangeFilter
    {
        private readonly string _localRootPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSyncRootChangeFilter" /> class.
        /// </summary>
        public LocalSyncRootChangeFilter(string localRootPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(localRootPath);
            _localRootPath = Path.GetFullPath(localRootPath);
        }

        /// <summary>
        /// Returns whether a watcher event should request a sync pass.
        /// </summary>
        public bool ShouldPublish(string fullPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
            string relativePath = Path.GetRelativePath(_localRootPath, Path.GetFullPath(fullPath));
            if (IsOutsideRoot(relativePath))
            {
                return false;
            }

            return string.Equals(relativePath, ".", StringComparison.Ordinal)
                || !LocalFileIgnoreRules.ShouldIgnore(relativePath);
        }

        /// <summary>
        /// Returns whether a rename event should request a sync pass.
        /// </summary>
        public bool ShouldPublishRename(string oldFullPath, string newFullPath)
        {
            return ShouldPublish(oldFullPath) || ShouldPublish(newFullPath);
        }

        private static bool IsOutsideRoot(string relativePath)
        {
            return Path.IsPathRooted(relativePath)
                || string.Equals(relativePath, "..", StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        }
    }
}
