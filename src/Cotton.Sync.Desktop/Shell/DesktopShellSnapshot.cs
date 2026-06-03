// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

using Cotton.Sync.Desktop.Platform;

internal sealed record DesktopShellSnapshot(
    Uri? ServerUrl,
    string? AccountName,
    string? RememberedUsername,
    bool StartWithOperatingSystem,
    DesktopPlatformCapabilitySnapshot PlatformCapabilities,
    bool IsSignedIn,
    IReadOnlyList<DesktopSyncPairSnapshot> SyncPairs);
