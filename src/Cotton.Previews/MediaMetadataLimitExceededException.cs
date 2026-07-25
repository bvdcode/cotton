// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    /// <summary>
    /// Indicates that extracted media tags exceeded a persistence boundary.
    /// </summary>
    public class MediaMetadataLimitExceededException(string limitName, int maxBytes)
        : Exception($"Media metadata exceeded the configured {limitName} limit of {maxBytes} bytes.")
    {
        /// <summary>
        /// Gets the exceeded metadata boundary name.
        /// </summary>
        public string LimitName { get; } = limitName;

        /// <summary>
        /// Gets the exceeded byte limit.
        /// </summary>
        public int MaxBytes { get; } = maxBytes;
    }
}
