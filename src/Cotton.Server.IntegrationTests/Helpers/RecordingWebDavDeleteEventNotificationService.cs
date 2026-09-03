// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Server.Services;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class RecordingWebDavDeleteEventNotificationService(
        WebDavDeleteEventRecorder recorder) : IEventNotificationService
    {
        public Task NotifyFileCreatedAsync(Guid nodeFileId, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyFileCreatedAsync(NodeFileManifestDto file, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyFileUpdatedAsync(Guid nodeFileId, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyFileUpdatedAsync(NodeFileManifestDto file, CancellationToken ct = default) => Task.CompletedTask;

        public Task NotifyFileDeletedAsync(Guid userId, Guid nodeFileId, Guid? parentNodeId, CancellationToken ct = default)
        {
            recorder.FileDeletedCount++;
            recorder.FileDeletedNodeFileId = nodeFileId;
            recorder.FileDeletedParentNodeId = parentNodeId;
            return Task.CompletedTask;
        }

        public Task NotifyFileMovedAsync(Guid nodeFileId, Guid oldParentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyFileRenamedAsync(Guid nodeFileId, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyFileRenamedAsync(NodeFileManifestDto file, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyFileRestoredAsync(Guid userId, Guid nodeFileId, NodeFileManifestDto? file, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyNodeCreatedAsync(Guid nodeId, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyNodeCreatedAsync(Guid userId, NodeDto node, CancellationToken ct = default) => Task.CompletedTask;

        public Task NotifyNodeDeletedAsync(Guid userId, Guid nodeId, Guid? parentNodeId, CancellationToken ct = default)
        {
            recorder.NodeDeletedCount++;
            recorder.NodeDeletedNodeId = nodeId;
            recorder.NodeDeletedParentNodeId = parentNodeId;
            return Task.CompletedTask;
        }

        public Task NotifyNodeMovedAsync(Guid nodeId, Guid oldParentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyNodeRenamedAsync(Guid nodeId, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyNodeRenamedAsync(Guid userId, NodeDto node, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyNodeMetadataUpdatedAsync(Guid userId, NodeDto node, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyNodeRestoredAsync(Guid userId, Guid nodeId, NodeDto? node, CancellationToken ct = default) => Task.CompletedTask;
    }
}
