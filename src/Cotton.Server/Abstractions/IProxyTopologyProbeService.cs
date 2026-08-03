// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Probes the configured public endpoint for additional reverse-proxy product hints.
    /// </summary>
    public interface IProxyTopologyProbeService
    {
        /// <summary>
        /// Returns service identifiers inferred from response headers, or an empty list when the probe fails.
        /// </summary>
        Task<IReadOnlyList<string>> DetectAsync(
            string publicBaseUrl,
            CancellationToken cancellationToken = default);
    }
}
