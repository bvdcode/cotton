// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal enum DesktopRunProgressStage
{
    Unknown = 0,
    ScanningLocal = 1,
    ScanningRemote = 2,
    ReconcilingDirectories = 3,
    ReconcilingFiles = 4,
    Completed = 5,
}
