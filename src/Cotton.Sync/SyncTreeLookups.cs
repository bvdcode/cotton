// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;

namespace Cotton.Sync
{
    internal class SyncTreeLookups
    {
        public SyncTreeLookups(
            Dictionary<string, LocalDirectorySnapshot> localDirectoriesByPath,
            Dictionary<string, RemoteDirectorySnapshot> remoteDirectoriesByPath,
            Dictionary<string, LocalFileSnapshot> localFilesByPath,
            Dictionary<string, RemoteFileSnapshot> remoteFilesByPath,
            NodeDto remoteRootNode)
        {
            LocalDirectoriesByPath = localDirectoriesByPath;
            RemoteDirectoriesByPath = remoteDirectoriesByPath;
            LocalFilesByPath = localFilesByPath;
            RemoteFilesByPath = remoteFilesByPath;
            RemoteRootNode = remoteRootNode;
        }

        public Dictionary<string, LocalDirectorySnapshot> LocalDirectoriesByPath { get; }

        public Dictionary<string, RemoteDirectorySnapshot> RemoteDirectoriesByPath { get; }

        public Dictionary<string, LocalFileSnapshot> LocalFilesByPath { get; }

        public Dictionary<string, RemoteFileSnapshot> RemoteFilesByPath { get; }

        public NodeDto RemoteRootNode { get; }
    }
}
