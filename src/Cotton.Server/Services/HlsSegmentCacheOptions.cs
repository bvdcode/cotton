// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    public class HlsSegmentCacheOptions
    {
        public long SizeLimitBytes { get; set; } = 512L * 1024 * 1024;
    }
}
