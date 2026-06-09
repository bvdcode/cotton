// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncPairs
{

    /// <summary>
    /// Describes one validation error for a sync-pair configuration.
    /// </summary>
    public sealed record SyncPairValidationError(
        SyncPairValidationIssue Issue,
        Guid? SyncPairId,
        Guid? OtherSyncPairId,
        string Message);
}
