// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;

namespace Cotton.Server.Services
{
    /// <summary>Stages durable sync feed rows without performing database I/O by itself.</summary>
    public class SyncChangeRecorder(CottonDbContext _dbContext) : ISyncChangeRecorder
    {
        /// <inheritdoc />
        public void StageFileChange(
            SyncChangeKind kind,
            NodeFile nodeFile,
            Guid layoutId,
            Guid? previousParentNodeId = null)
        {
            EnsureKnownKind(kind);

            _dbContext.SyncChanges.Add(new SyncChange
            {
                OwnerId = nodeFile.OwnerId,
                Kind = kind,
                LayoutId = layoutId,
                ItemId = nodeFile.Id,
                ParentNodeId = nodeFile.NodeId,
                PreviousParentNodeId = previousParentNodeId,
                FileManifestId = nodeFile.FileManifestId,
                Name = nodeFile.Name,
            });
        }

        /// <inheritdoc />
        public void StageFolderChange(
            SyncChangeKind kind,
            Node node,
            Guid? previousParentNodeId = null)
        {
            EnsureKnownKind(kind);

            _dbContext.SyncChanges.Add(new SyncChange
            {
                OwnerId = node.OwnerId,
                Kind = kind,
                LayoutId = node.LayoutId,
                ItemId = node.Id,
                ParentNodeId = node.ParentId
                    ?? throw new InvalidOperationException("Root nodes cannot be recorded as sync folder changes."),
                PreviousParentNodeId = previousParentNodeId,
                Name = node.Name,
            });
        }
    }
}
