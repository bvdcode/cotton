// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Lists the supported hls rendition values.
    /// </summary>
    public enum HlsRendition
    {
        /// <summary>
        /// Represents the source option.
        /// </summary>
        Source,
        /// <summary>
        /// Represents the high option.
        /// </summary>
        High,
        /// <summary>
        /// Represents the medium option.
        /// </summary>
        Medium,
        /// <summary>
        /// Represents the low option.
        /// </summary>
        Low,
    }
}
