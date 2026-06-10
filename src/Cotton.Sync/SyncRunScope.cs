// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync
{
    /// <summary>
    /// Defines the filesystem surface that a sync pass must reconcile.
    /// </summary>
    public sealed class SyncRunScope
    {
        private SyncRunScope(bool isFull, IReadOnlyList<string> localChangedPaths)
        {
            IsFull = isFull;
            LocalChangedPaths = localChangedPaths;
        }

        /// <summary>
        /// Gets a scope that reconciles the whole sync pair.
        /// </summary>
        public static SyncRunScope Full { get; } = new(true, Array.Empty<string>());

        /// <summary>
        /// Gets a value indicating whether the run must reconcile the whole sync pair.
        /// </summary>
        public bool IsFull { get; }

        /// <summary>
        /// Gets normalized local relative paths that triggered this pass.
        /// </summary>
        public IReadOnlyList<string> LocalChangedPaths { get; }

        /// <summary>
        /// Creates a scope for local changed paths.
        /// </summary>
        public static SyncRunScope ForLocalChangedPaths(IEnumerable<string> relativePaths)
        {
            ArgumentNullException.ThrowIfNull(relativePaths);
            List<string> paths = relativePaths
                .Select(SyncPath.Normalize)
                .Where(static path => !string.IsNullOrWhiteSpace(path) && !SyncPathIgnoreRules.ShouldIgnore(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return paths.Count == 0 ? Full : new SyncRunScope(false, paths);
        }
    }
}
