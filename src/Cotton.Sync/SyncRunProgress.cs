// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync
{
    /// <summary>
    /// Describes aggregate progress for one synchronization pass.
    /// </summary>
    public sealed class SyncRunProgress
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyncRunProgress" /> class.
        /// </summary>
        public SyncRunProgress(
            SyncRunProgressStage stage,
            int filesCompleted,
            int? filesTotal,
            string? currentPath,
            DateTime startedAtUtc,
            bool isCompleted = false,
            long bytesCompleted = 0,
            long? bytesTotal = null)
        {
            if (stage == SyncRunProgressStage.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(stage), "Sync progress stage must be known.");
            }

            ArgumentOutOfRangeException.ThrowIfNegative(filesCompleted);
            if (filesTotal.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(filesTotal.Value);
                if (filesCompleted > filesTotal.Value)
                {
                    throw new ArgumentOutOfRangeException(nameof(filesCompleted), "Completed file count cannot exceed total file count.");
                }
            }

            ArgumentOutOfRangeException.ThrowIfNegative(bytesCompleted);
            if (bytesTotal.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(bytesTotal.Value);
                if (bytesCompleted > bytesTotal.Value)
                {
                    throw new ArgumentOutOfRangeException(nameof(bytesCompleted), "Completed byte count cannot exceed total byte count.");
                }
            }

            Stage = stage;
            FilesCompleted = filesCompleted;
            FilesTotal = filesTotal;
            CurrentPath = string.IsNullOrWhiteSpace(currentPath) ? string.Empty : SyncPath.Normalize(currentPath);
            StartedAtUtc = startedAtUtc.ToUniversalTime();
            IsCompleted = isCompleted;
            BytesCompleted = bytesCompleted;
            BytesTotal = bytesTotal;
            OccurredAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the current synchronization stage.
        /// </summary>
        public SyncRunProgressStage Stage { get; }

        /// <summary>
        /// Gets the number of file entries already reconciled in this pass.
        /// </summary>
        public int FilesCompleted { get; }

        /// <summary>
        /// Gets the total number of file entries to reconcile when known.
        /// </summary>
        public int? FilesTotal { get; }

        /// <summary>
        /// Gets the normalized file path currently being reconciled, when available.
        /// </summary>
        public string CurrentPath { get; }

        /// <summary>
        /// Gets the UTC timestamp when this sync pass started.
        /// </summary>
        public DateTime StartedAtUtc { get; }

        /// <summary>
        /// Gets a value indicating whether the sync pass completed.
        /// </summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// Gets the number of transfer bytes already completed in this pass.
        /// </summary>
        public long BytesCompleted { get; }

        /// <summary>
        /// Gets the total transfer bytes planned for this pass when known.
        /// </summary>
        public long? BytesTotal { get; }

        /// <summary>
        /// Gets the UTC timestamp when this progress sample was produced.
        /// </summary>
        public DateTime OccurredAtUtc { get; }
    }
}
