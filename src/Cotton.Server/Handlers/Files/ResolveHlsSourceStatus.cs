// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Describes the outcome of resolving an HLS source.
    /// </summary>
    public enum ResolveHlsSourceStatus
    {
        /// <summary>
        /// The file can be transcoded for HLS playback.
        /// </summary>
        Success,

        /// <summary>
        /// The download token was not found or is no longer active.
        /// </summary>
        TokenNotFound,

        /// <summary>
        /// The requested file was not found.
        /// </summary>
        FileNotFound,

        /// <summary>
        /// The file does not require on-the-fly transcoding.
        /// </summary>
        NotTranscodable,
    }
}
