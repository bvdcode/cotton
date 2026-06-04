// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.State;

namespace Cotton.Sync.App.Progress;

/// <summary>
/// Describes live transfer progress for one sync pair.
/// </summary>
public sealed class AppTransferProgress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppTransferProgress" /> class.
    /// </summary>
    public AppTransferProgress(
        Guid syncPairId,
        AppTransferDirection direction,
        string relativePath,
        long transferredBytes,
        long? totalBytes,
        bool isCompleted,
        DateTime occurredAtUtc)
    {
        if (direction == AppTransferDirection.Unknown)
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

        SyncPairId = syncPairId;
        Direction = direction;
        RelativePath = relativePath.Trim();
        TransferredBytes = transferredBytes;
        TotalBytes = totalBytes;
        IsCompleted = isCompleted;
        OccurredAtUtc = UtcDateTime.Normalize(occurredAtUtc);
    }

    /// <summary>
    /// Gets the sync pair identifier.
    /// </summary>
    public Guid SyncPairId { get; }

    /// <summary>
    /// Gets the transfer direction.
    /// </summary>
    public AppTransferDirection Direction { get; }

    /// <summary>
    /// Gets the relative item path.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets bytes processed for the current transfer.
    /// </summary>
    public long TransferredBytes { get; }

    /// <summary>
    /// Gets the total transfer size when known.
    /// </summary>
    public long? TotalBytes { get; }

    /// <summary>
    /// Gets a value indicating whether the current transfer has completed.
    /// </summary>
    public bool IsCompleted { get; }

    /// <summary>
    /// Gets the UTC timestamp when the progress sample was produced.
    /// </summary>
    public DateTime OccurredAtUtc { get; }
}
