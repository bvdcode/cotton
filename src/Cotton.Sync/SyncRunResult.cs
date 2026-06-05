// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync;

/// <summary>
/// Contains the activities emitted by one synchronization pass.
/// </summary>
public sealed class SyncRunResult
{
    /// <summary>
    /// Gets the activities emitted during the pass.
    /// </summary>
    public List<SyncActivity> Activities { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the pass stopped on a condition that needs user review.
    /// </summary>
    public bool RequiresUserAction => Activities.Any(static activity => activity.RequiresUserAction);

    /// <summary>
    /// Gets the first user-review message reported by the pass.
    /// </summary>
    public string? ActionRequiredMessage => Activities
        .FirstOrDefault(static activity => activity.RequiresUserAction)
        ?.Details;
}
