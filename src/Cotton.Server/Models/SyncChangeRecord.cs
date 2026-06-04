// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;

namespace Cotton.Server.Models
{
    /// <summary>Captured file-tree mutation data for the durable sync feed.</summary>
    public readonly record struct SyncChangeRecord(
        Guid OwnerId,
        SyncChangeKind Kind,
        Guid LayoutId,
        Guid ItemId,
        Guid ParentNodeId,
        string Name,
        Guid? PreviousParentNodeId = null,
        Guid? FileManifestId = null);
}
