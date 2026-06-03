// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.State;

namespace Cotton.Sync.App.Status;

/// <summary>
/// Describes the current user-visible desktop sync application status.
/// </summary>
public sealed class SyncAppStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncAppStatus" /> class.
    /// </summary>
    public SyncAppStatus(
        bool isAuthenticated,
        IEnumerable<SyncPairStatus> syncPairs,
        DateTime updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(syncPairs);
        IsAuthenticated = isAuthenticated;
        SyncPairs = syncPairs.ToList();
        UpdatedAtUtc = UtcDateTime.Normalize(updatedAtUtc);
    }

    /// <summary>
    /// Gets a value indicating whether the application currently has an authenticated session.
    /// </summary>
    public bool IsAuthenticated { get; }

    /// <summary>
    /// Gets current per-pair statuses.
    /// </summary>
    public IReadOnlyList<SyncPairStatus> SyncPairs { get; }

    /// <summary>
    /// Gets the UTC timestamp when this status was updated.
    /// </summary>
    public DateTime UpdatedAtUtc { get; }

    /// <summary>
    /// Creates the default unauthenticated app status.
    /// </summary>
    public static SyncAppStatus CreateEmpty()
    {
        return new SyncAppStatus(false, [], DateTime.UtcNow);
    }
}
