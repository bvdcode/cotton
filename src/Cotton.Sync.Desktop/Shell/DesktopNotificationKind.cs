// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell
{

    internal enum DesktopNotificationKind
    {
        Unknown = 0,
        InitialSyncComplete = 1,
        Conflict = 2,
        ActionRequiredError = 3,
    }
}
