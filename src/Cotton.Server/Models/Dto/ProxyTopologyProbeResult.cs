// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Contains normalized service and location hints returned by a proxy topology probe.
    /// </summary>
    public sealed record ProxyTopologyProbeResult(
        IReadOnlyList<string> Services,
        CloudflareProxyMetadataDto? Cloudflare);
}
