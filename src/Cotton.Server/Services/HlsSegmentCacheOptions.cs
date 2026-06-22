// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Configures hls segment cache.
    /// </summary>
    public class HlsSegmentCacheOptions
    {
        /// <summary>
        /// Gets or sets the size limit bytes.
        /// </summary>
        public long SizeLimitBytes { get; set; } = 512L * 1024 * 1024;
    }
}
