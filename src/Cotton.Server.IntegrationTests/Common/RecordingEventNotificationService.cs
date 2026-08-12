// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Server.Services;

namespace Cotton.Server.IntegrationTests.Common
{
    public class RecordingEventNotificationService : IEventNotificationService
    {
        private int _fileCreatedCount;
        private int _fileUpdatedCount;
        private int _fileDeletedCount;
        private int _fileMovedCount;
        private int _fileRenamedCount;
        private int _fileRestoredCount;
        private int _nodeCreatedCount;
        private int _nodeDeletedCount;
        private int _nodeMovedCount;
        private int _nodeRenamedCount;
        private int _nodeMetadataUpdatedCount;
        private int _nodeRestoredCount;

        public int FileCreatedCount => _fileCreatedCount;
        public int FileUpdatedCount => _fileUpdatedCount;
        public int FileDeletedCount => _fileDeletedCount;
        public int FileMovedCount => _fileMovedCount;
        public int FileRenamedCount => _fileRenamedCount;
        public int FileRestoredCount => _fileRestoredCount;
        public int NodeCreatedCount => _nodeCreatedCount;
        public int NodeDeletedCount => _nodeDeletedCount;
        public int NodeMovedCount => _nodeMovedCount;
        public int NodeRenamedCount => _nodeRenamedCount;
        public int NodeMetadataUpdatedCount => _nodeMetadataUpdatedCount;
        public int NodeRestoredCount => _nodeRestoredCount;

        public Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default)
            => Record(ref _fileCreatedCount);

        public Task NotifyFileCreatedAsync(NodeFileManifestDto file, CancellationToken ct = default)
            => Record(ref _fileCreatedCount);

        public Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default)
            => Record(ref _fileUpdatedCount);

        public Task NotifyFileUpdatedAsync(NodeFileManifestDto file, CancellationToken ct = default)
            => Record(ref _fileUpdatedCount);

        public Task NotifyFileDeletedAsync(
            Guid userId,
            Guid nodeFileId,
            Guid? parentNodeId,
            CancellationToken ct = default)
            => Record(ref _fileDeletedCount);

        public Task NotifyFileMovedAsync(
            Guid nodeFileId,
            Guid oldParentId,
            CancellationToken ct = default)
            => Record(ref _fileMovedCount);

        public Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default)
            => Record(ref _fileRenamedCount);

        public Task NotifyFileRenamedAsync(NodeFileManifestDto file, CancellationToken ct = default)
            => Record(ref _fileRenamedCount);

        public Task NotifyFileRestoredAsync(
            Guid userId,
            Guid nodeFileId,
            NodeFileManifestDto? file,
            CancellationToken ct = default)
            => Record(ref _fileRestoredCount);

        public Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default)
            => Record(ref _nodeCreatedCount);

        public Task NotifyNodeCreatedAsync(
            Guid userId,
            NodeDto node,
            CancellationToken ct = default)
            => Record(ref _nodeCreatedCount);

        public Task NotifyNodeDeletedAsync(
            Guid userId,
            Guid nodeId,
            Guid? parentNodeId,
            CancellationToken ct = default)
            => Record(ref _nodeDeletedCount);

        public Task NotifyNodeMovedAsync(
            Guid nodeId,
            Guid oldParentId,
            CancellationToken ct = default)
            => Record(ref _nodeMovedCount);

        public Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default)
            => Record(ref _nodeRenamedCount);

        public Task NotifyNodeRenamedAsync(
            Guid userId,
            NodeDto node,
            CancellationToken ct = default)
            => Record(ref _nodeRenamedCount);

        public Task NotifyNodeMetadataUpdatedAsync(
            Guid userId,
            NodeDto node,
            CancellationToken ct = default)
            => Record(ref _nodeMetadataUpdatedCount);

        public Task NotifyNodeRestoredAsync(
            Guid userId,
            Guid nodeId,
            NodeDto? node,
            CancellationToken ct = default)
            => Record(ref _nodeRestoredCount);

        private static Task Record(ref int count)
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        }
    }
}
