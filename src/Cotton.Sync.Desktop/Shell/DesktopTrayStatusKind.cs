// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell
{
    internal enum DesktopTrayStatusKind
    {
        Unknown = 0,
        SignedOut = 1,
        Idle = 2,
        Syncing = 3,
        Paused = 4,
        Offline = 5,
        Error = 6,
    }
}
