// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Sdk;
using Cotton.Sdk.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.SyncPairs
{
    /// <summary>
    /// Checks remote sync roots through the Cotton SDK.
    /// </summary>
    public sealed class SdkRemoteSyncRootProbe : IRemoteSyncRootProbe
    {
        private readonly ILogger<SdkRemoteSyncRootProbe> _logger;
        private readonly ICottonNodeClient _nodes;

        /// <summary>
        /// Initializes a new instance of the <see cref="SdkRemoteSyncRootProbe" /> class.
        /// </summary>
        public SdkRemoteSyncRootProbe(
            ICottonNodeClient nodes,
            ILogger<SdkRemoteSyncRootProbe>? logger = null)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _logger = logger ?? NullLogger<SdkRemoteSyncRootProbe>.Instance;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(Guid remoteRootNodeId, CancellationToken cancellationToken = default)
        {
            try
            {
                _ = await _nodes.GetAsync(remoteRootNodeId, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (CottonApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    exception,
                    "Remote sync root is unavailable: {RemoteRootNodeId}",
                    remoteRootNodeId);
                return false;
            }
        }
    }
}
