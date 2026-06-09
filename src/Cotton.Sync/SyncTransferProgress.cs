// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync
{
    /// <summary>
    /// Describes live progress for one file transfer.
    /// </summary>
    public sealed class SyncTransferProgress
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyncTransferProgress" /> class.
        /// </summary>
        public SyncTransferProgress(
            SyncTransferDirection direction,
            string relativePath,
            long transferredBytes,
            long? totalBytes,
            bool isCompleted = false)
        {
            if (direction == SyncTransferDirection.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), "Transfer direction must be known.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
            ArgumentOutOfRangeException.ThrowIfNegative(transferredBytes);
            if (totalBytes.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(totalBytes.Value);
                if (transferredBytes > totalBytes.Value)
                {
                    throw new ArgumentOutOfRangeException(nameof(transferredBytes), "Transferred bytes cannot exceed total bytes.");
                }
            }

            Direction = direction;
            RelativePath = SyncPath.Normalize(relativePath);
            TransferredBytes = transferredBytes;
            TotalBytes = totalBytes;
            IsCompleted = isCompleted;
            OccurredAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the transfer direction.
        /// </summary>
        public SyncTransferDirection Direction { get; }

        /// <summary>
        /// Gets the normalized relative path associated with the transfer.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the number of bytes already processed for this transfer.
        /// </summary>
        public long TransferredBytes { get; }

        /// <summary>
        /// Gets the total file size in bytes when known.
        /// </summary>
        public long? TotalBytes { get; }

        /// <summary>
        /// Gets a value indicating whether this file transfer has completed.
        /// </summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// Gets the UTC timestamp when the progress sample was produced.
        /// </summary>
        public DateTime OccurredAtUtc { get; }
    }
}
