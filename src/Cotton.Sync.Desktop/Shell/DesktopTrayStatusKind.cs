// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal enum DesktopTrayStatusKind
{
    SignedOut = 0,
    Idle = 1,
    Syncing = 2,
    Paused = 3,
    Offline = 4,
    Error = 5,
}
