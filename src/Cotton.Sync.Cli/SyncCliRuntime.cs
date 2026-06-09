// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk;
using Cotton.Sync.State;

namespace Cotton.Sync.Cli
{
    internal record SyncCliRuntime(
        SyncPair SyncPair,
        SqliteSyncStateStore StateStore,
        SyncEngine Engine,
        ICottonCloudClient Client) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return Client.DisposeAsync();
        }
    }
}
