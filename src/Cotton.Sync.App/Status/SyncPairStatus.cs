// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.State;

namespace Cotton.Sync.App.Status
{

    /// <summary>
    /// Describes current user-visible status for one sync pair.
    /// </summary>
    public sealed class SyncPairStatus
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyncPairStatus" /> class.
        /// </summary>
        public SyncPairStatus(
            Guid syncPairId,
            string displayName,
            SyncPairRunState state,
            string? currentOperation,
            string? lastError,
            DateTime updatedAtUtc,
            DateTime? lastSuccessfulSyncAtUtc = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
            ArgumentOutOfRangeException.ThrowIfEqual(state, SyncPairRunState.Unknown);
            SyncPairId = syncPairId;
            DisplayName = displayName;
            State = state;
            CurrentOperation = currentOperation;
            LastError = lastError;
            UpdatedAtUtc = UtcDateTime.Normalize(updatedAtUtc);
            LastSuccessfulSyncAtUtc = lastSuccessfulSyncAtUtc.HasValue
                ? UtcDateTime.Normalize(lastSuccessfulSyncAtUtc.Value)
                : null;
        }

        /// <summary>
        /// Gets the sync pair identifier.
        /// </summary>
        public Guid SyncPairId { get; }

        /// <summary>
        /// Gets the user-facing sync pair name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the current runtime state.
        /// </summary>
        public SyncPairRunState State { get; }

        /// <summary>
        /// Gets the current operation text, if any.
        /// </summary>
        public string? CurrentOperation { get; }

        /// <summary>
        /// Gets the latest action-required error text, if any.
        /// </summary>
        public string? LastError { get; }

        /// <summary>
        /// Gets the UTC timestamp when this status was updated.
        /// </summary>
        public DateTime UpdatedAtUtc { get; }

        /// <summary>
        /// Gets the UTC timestamp of the latest successful sync pass, if any.
        /// </summary>
        public DateTime? LastSuccessfulSyncAtUtc { get; }
    }
}
