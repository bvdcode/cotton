// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.SyncApplication
{
    /// <summary>
    /// Describes the outcome of saving sync-pair settings.
    /// </summary>
    public sealed class SyncPairSaveResult
    {
        private SyncPairSaveResult(bool isSaved, SyncPairValidationResult validation)
        {
            IsSaved = isSaved;
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
        }

        /// <summary>
        /// Gets a value indicating whether the sync pair was persisted.
        /// </summary>
        public bool IsSaved { get; }

        /// <summary>
        /// Gets the validation result for the attempted save.
        /// </summary>
        public SyncPairValidationResult Validation { get; }

        /// <summary>
        /// Creates a successful save result.
        /// </summary>
        public static SyncPairSaveResult Saved(SyncPairValidationResult validation)
        {
            return new SyncPairSaveResult(true, validation);
        }

        /// <summary>
        /// Creates a rejected save result.
        /// </summary>
        public static SyncPairSaveResult Rejected(SyncPairValidationResult validation)
        {
            return new SyncPairSaveResult(false, validation);
        }
    }
}
