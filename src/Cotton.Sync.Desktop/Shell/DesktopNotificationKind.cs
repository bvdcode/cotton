// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal enum DesktopNotificationKind
{
    InitialSyncComplete = 0,
    Conflict = 1,
    ActionRequiredError = 2,
}
