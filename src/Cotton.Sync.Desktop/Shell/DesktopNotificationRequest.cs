// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal sealed record DesktopNotificationRequest(
    DesktopNotificationKind Kind,
    Guid SyncPairId,
    string Title,
    string Message);
