// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Probes the configured public endpoint for additional reverse-proxy product hints.
    /// </summary>
    public interface IProxyTopologyProbeService
    {
        /// <summary>
        /// Returns normalized topology hints inferred from response headers, or an empty result when the probe fails.
        /// </summary>
        Task<ProxyTopologyProbeResult> DetectAsync(
            string publicBaseUrl,
            CancellationToken cancellationToken = default);
    }
}
