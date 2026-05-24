// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the node file manifest API payload.
    /// </summary>
    public class NodeFileManifestDto : BaseDto<Guid>
    {
        /// <summary>
        /// Gets or sets node id.
        /// </summary>
        public Guid NodeId { get; set; }
        /// <summary>
        /// Gets or sets owner id.
        /// </summary>
        public Guid OwnerId { get; set; }
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

        private Dictionary<string, string> _metadata = [];
        /// <summary>
        /// Gets or sets structured metadata attached to the resource.
        /// </summary>
        public Dictionary<string, string> Metadata
        {
            get => _metadata;
            set => _metadata = value ?? [];
        }

        /// <summary>
        /// Gets or sets requires video transcoding.
        /// </summary>
        public bool RequiresVideoTranscoding { get; set; }

        /// <summary>
        /// Gets or sets preview hash encrypted hex.
        /// </summary>
        public string? PreviewHashEncryptedHex { get; set; }
    }
}
