// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the archive download link API payload.
    /// </summary>
    public sealed class ArchiveDownloadLinkDto
    {
        /// <summary>
        /// Gets or sets url.
        /// </summary>
        public string Url { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets file name.
        /// </summary>
        public string FileName { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets size bytes.
        /// </summary>
        public long SizeBytes { get; init; }
        /// <summary>
        /// Gets or sets entry count.
        /// </summary>
        public int EntryCount { get; init; }
    }
}
