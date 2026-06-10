// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Runners
{
    /// <summary>
    /// Describes the sync surface requested by an application coordinator.
    /// </summary>
    public sealed class SyncRunRequest
    {
        private SyncRunRequest(bool isFull, IReadOnlyList<string> localChangedPaths)
        {
            IsFull = isFull;
            LocalChangedPaths = localChangedPaths;
        }

        /// <summary>
        /// Gets a request that reconciles the whole sync pair.
        /// </summary>
        public static SyncRunRequest Full { get; } = new(true, Array.Empty<string>());

        /// <summary>
        /// Gets a value indicating whether the whole sync pair must be reconciled.
        /// </summary>
        public bool IsFull { get; }

        /// <summary>
        /// Gets local relative paths that should be reconciled.
        /// </summary>
        public IReadOnlyList<string> LocalChangedPaths { get; }

        /// <summary>
        /// Creates a local-path request.
        /// </summary>
        public static SyncRunRequest ForLocalChangedPaths(IEnumerable<string> relativePaths)
        {
            ArgumentNullException.ThrowIfNull(relativePaths);
            List<string> paths = relativePaths
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return paths.Count == 0 ? Full : new SyncRunRequest(false, paths);
        }

        /// <summary>
        /// Merges two requests without losing a required full reconcile.
        /// </summary>
        public SyncRunRequest Merge(SyncRunRequest other)
        {
            ArgumentNullException.ThrowIfNull(other);
            if (IsFull || other.IsFull)
            {
                return Full;
            }

            return ForLocalChangedPaths(LocalChangedPaths.Concat(other.LocalChangedPaths));
        }
    }
}
