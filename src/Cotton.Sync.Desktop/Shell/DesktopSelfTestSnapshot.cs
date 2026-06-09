// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell
{
    internal record DesktopSelfTestSnapshot(
        IReadOnlyList<DesktopSelfTestItemSnapshot> Items)
    {
        public bool Passed => Items.All(static item => item.Passed);
    }
}
