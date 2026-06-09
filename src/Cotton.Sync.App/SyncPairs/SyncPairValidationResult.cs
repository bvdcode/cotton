// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs
{
    /// <summary>
    /// Contains validation errors for a set of sync-pair settings.
    /// </summary>
    public class SyncPairValidationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyncPairValidationResult" /> class.
        /// </summary>
        public SyncPairValidationResult(IReadOnlyList<SyncPairValidationError> errors)
        {
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        /// <summary>
        /// Gets a value indicating whether the settings are valid.
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Gets the validation errors.
        /// </summary>
        public IReadOnlyList<SyncPairValidationError> Errors { get; }
    }
}
