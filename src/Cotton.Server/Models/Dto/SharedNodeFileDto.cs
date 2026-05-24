// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the shared node file API payload.
    /// </summary>
    public class SharedNodeFileDto : BaseDto<Guid>
    {
        /// <summary>
        /// Gets or sets node id.
        /// </summary>
        public Guid NodeId { get; set; }
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = null!;
        /// <summary>
        /// Gets or sets content type.
        /// </summary>
        public string ContentType { get; set; } = null!;
        /// <summary>
        /// Gets or sets size bytes.
        /// </summary>
        public long SizeBytes { get; set; }
        /// <summary>
        /// Gets or sets preview hash encrypted hex.
        /// </summary>
        public string? PreviewHashEncryptedHex { get; set; }
    }
}
