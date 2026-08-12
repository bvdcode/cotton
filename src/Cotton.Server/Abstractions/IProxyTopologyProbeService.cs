// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

namespace Cotton.Server.Abstractions
{
    public interface IProxyTopologyProbeService
    {
        Task<ProxyTopologyProbeResult> DetectAsync(
            string publicBaseUrl,
            CancellationToken cancellationToken = default);
    }
}
