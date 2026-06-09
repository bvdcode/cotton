// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.State;

namespace Cotton.Sync.App.Progress
{
    /// <summary>
    /// Describes aggregate synchronization-pass progress for one sync pair.
    /// </summary>
    public class AppRunProgress
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppRunProgress" /> class.
        /// </summary>
        public AppRunProgress(
            Guid syncPairId,
            SyncRunProgressStage stage,
            int filesCompleted,
            int? filesTotal,
            string currentPath,
            DateTime startedAtUtc,
            bool isCompleted,
            DateTime occurredAtUtc,
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

            SyncPairId = syncPairId;
            Stage = stage;
            FilesCompleted = filesCompleted;
            FilesTotal = filesTotal;
            CurrentPath = currentPath.Trim();
            StartedAtUtc = UtcDateTime.Normalize(startedAtUtc);
            IsCompleted = isCompleted;
            OccurredAtUtc = UtcDateTime.Normalize(occurredAtUtc);
            BytesCompleted = bytesCompleted;
            BytesTotal = bytesTotal;
        }

        /// <summary>
        /// Gets the sync pair identifier.
        /// </summary>
        public Guid SyncPairId { get; }

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
        /// Gets the current file path when available.
        /// </summary>
        public string CurrentPath { get; }

        /// <summary>
        /// Gets the UTC timestamp when the sync pass started.
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
