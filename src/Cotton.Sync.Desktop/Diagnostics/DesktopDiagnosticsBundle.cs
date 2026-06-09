// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Diagnostics
{

    internal sealed record DesktopDiagnosticsBundle(
        DateTimeOffset CreatedAtUtc,
        string AppVersion,
        string? ServerUrl,
        string AccountName,
        DesktopDataPathSnapshot DataPaths,
        IReadOnlyList<DesktopSyncPairSnapshot> SyncPairs,
        IReadOnlyList<DesktopSelfTestItemSnapshot> SelfTestItems);
}
