// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Cli;

internal record SyncCliConnectionOptions(
    Uri ServerUri,
    string? Username,
    string? Password,
    string LocalRoot,
    Guid RemoteRootNodeId,
    string SyncPairId,
    string DatabasePath,
    string? TwoFactorCode,
    bool UseBrowserLogin);
