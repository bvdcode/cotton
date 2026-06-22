// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Lists the supported video playback mode values.
    /// </summary>
    public enum VideoPlaybackMode
    {
        /// <summary>
        /// Represents the none option.
        /// </summary>
        None,
        /// <summary>
        /// Represents the native option.
        /// </summary>
        Native,
        /// <summary>
        /// Represents the transcode option.
        /// </summary>
        Transcode,
        /// <summary>
        /// Represents the unsupported option.
        /// </summary>
        Unsupported,
    }
}
