// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Models.Enums;

namespace Cotton.Server.Abstractions
{
    public interface ISyncChangeRecorder
    {
        void StageFileChange(
            SyncChangeKind kind,
            NodeFile nodeFile,
            Guid layoutId,
            Guid? previousParentNodeId = null);

        void StageFolderChange(
            SyncChangeKind kind,
            Node node,
            Guid parentNodeId,
            Guid? previousParentNodeId = null);
    }
}
