// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.State;

namespace Cotton.Sync.App.Activities;

/// <summary>
/// Represents one recent sync activity entry.
/// </summary>
public sealed class AppSyncActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppSyncActivity" /> class.
    /// </summary>
    public AppSyncActivity(
        Guid id,
        Guid syncPairId,
        SyncActivityKind type,
        string? itemPath,
        string message,
        DateTime occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (type == SyncActivityKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Activity type must be known.");
        }

        Id = id;
        SyncPairId = syncPairId;
        Type = type;
        ItemPath = itemPath;
        Message = message;
        OccurredAtUtc = UtcDateTime.Normalize(occurredAtUtc);
    }

    /// <summary>
    /// Gets the activity identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the sync pair identifier associated with this activity.
    /// </summary>
    public Guid SyncPairId { get; }

    /// <summary>
    /// Gets the activity type.
    /// </summary>
    public SyncActivityKind Type { get; }

    /// <summary>
    /// Gets the optional local or remote item path.
    /// </summary>
    public string? ItemPath { get; }

    /// <summary>
    /// Gets the user-visible activity message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the UTC timestamp when this activity occurred.
    /// </summary>
    public DateTime OccurredAtUtc { get; }
}
