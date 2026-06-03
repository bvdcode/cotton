// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal sealed record DesktopSyncPairStatusSnapshot(
    Guid Id,
    string Status,
    string? LastError,
    string? CurrentOperation = null);
