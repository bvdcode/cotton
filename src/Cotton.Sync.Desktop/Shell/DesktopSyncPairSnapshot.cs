// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.Desktop.Shell
{
    internal record DesktopSyncPairSnapshot(
        Guid Id,
        string DisplayName,
        string LocalPath,
        string RemotePath,
        string Status,
        Guid? RemoteRootNodeId = null,
        DateTime? LastSyncedAtUtc = null,
        long? ChangeCursor = null,
        string? LastError = null,
        SyncPairMode Mode = SyncPairMode.FullMirror);
}
