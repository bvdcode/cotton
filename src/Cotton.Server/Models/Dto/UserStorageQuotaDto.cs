// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the user storage quota API payload.
    /// </summary>
    public class UserStorageQuotaDto
    {
        /// <summary>
        /// Gets or sets used bytes.
        /// </summary>
        public long UsedBytes { get; set; }
        /// <summary>
        /// Gets or sets quota bytes.
        /// </summary>
        public long? QuotaBytes { get; set; }
        /// <summary>
        /// Gets or sets available bytes.
        /// </summary>
        public long? AvailableBytes { get; set; }
    }
}
