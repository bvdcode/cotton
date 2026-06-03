// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal sealed record DesktopSyncPairSnapshot(
    Guid Id,
    string DisplayName,
    string LocalPath,
    string RemotePath,
    string Status);
