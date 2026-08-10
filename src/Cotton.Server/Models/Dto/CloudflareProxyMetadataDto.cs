// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Contains normalized, non-sensitive Cloudflare request-location hints.
    /// </summary>
    public sealed class CloudflareProxyMetadataDto
    {
        /// <summary>
        /// Gets the two-character visitor country code reported by Cloudflare, including special XX and T1 values.
        /// </summary>
        public string? VisitorCountryCode { get; init; }

        /// <summary>
        /// Gets the three-character IATA code of the Cloudflare data center reported by CF-Ray.
        /// </summary>
        public string? DatacenterCode { get; init; }
    }
}
