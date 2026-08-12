// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class CloudflareProxyMetadataDto
    {
        public string? VisitorCountryCode { get; init; }

        public string? DatacenterCode { get; init; }
    }
}
