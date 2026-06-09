// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.SyncApplication
{
    internal record StartedSyncComponent(
        string Name,
        Func<CancellationToken, Task> StopAsync);
}
