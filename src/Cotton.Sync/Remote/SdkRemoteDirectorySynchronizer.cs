// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;
using Cotton.Sdk.Nodes;

namespace Cotton.Sync.Remote
{

    /// <summary>
    /// Performs remote directory mutations through the Cotton SDK node API.
    /// </summary>
    public sealed class SdkRemoteDirectorySynchronizer : IRemoteDirectorySynchronizer
    {
        private readonly ICottonNodeClient _nodes;

        /// <summary>
        /// Initializes a new instance of the <see cref="SdkRemoteDirectorySynchronizer" /> class.
        /// </summary>
        public SdkRemoteDirectorySynchronizer(ICottonNodeClient nodes)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        }

        /// <inheritdoc />
        public Task<NodeDto> CreateDirectoryAsync(
            Guid parentNodeId,
            string name,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return _nodes.CreateAsync(parentNodeId, name.Trim(), cancellationToken);
        }

        /// <inheritdoc />
        public Task DeleteDirectoryAsync(
            Guid nodeId,
            bool skipTrash = false,
            CancellationToken cancellationToken = default)
        {
            return _nodes.DeleteAsync(nodeId, skipTrash, cancellationToken);
        }
    }
}
