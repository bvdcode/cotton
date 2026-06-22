// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Models.Enums;

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Records sync feed rows in the current database unit of work.
    /// </summary>
    public interface ISyncChangeRecorder
    {
        /// <summary>
        /// Stages a file snapshot mutation for the caller's next database save.
        /// </summary>
        void StageFileChange(
            SyncChangeKind kind,
            NodeFile nodeFile,
            Guid layoutId,
            Guid? previousParentNodeId = null);

        /// <summary>
        /// Stages a folder snapshot mutation for the caller's next database save.
        /// </summary>
        void StageFolderChange(
            SyncChangeKind kind,
            Node node,
            Guid parentNodeId,
            Guid? previousParentNodeId = null);
    }
}
