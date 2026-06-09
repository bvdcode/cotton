// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync.Cli
{

    internal record SyncCliPassResult(
        SyncRunResult Result,
        IReadOnlyList<SyncStateEntry> StateEntries);
}
