// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal sealed record DesktopRunProgressSnapshot(
    Guid SyncPairId,
    SyncRunProgressStage Stage,
    int FilesCompleted,
    int? FilesTotal,
    string CurrentPath,
    DateTime StartedAtUtc,
    bool IsCompleted,
    DateTime OccurredAtUtc);
