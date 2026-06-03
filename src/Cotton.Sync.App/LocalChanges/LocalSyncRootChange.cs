// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.LocalChanges;

/// <summary>
/// Represents a local filesystem change under a configured sync root.
/// </summary>
public sealed record LocalSyncRootChange(
    Guid SyncPairId,
    string FullPath,
    LocalSyncRootChangeKind Kind);
