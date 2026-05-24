// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the node API payload.
    /// </summary>
    public class NodeDto : BaseDto<Guid>
    {
        /// <summary>
        /// Gets or sets layout id.
        /// </summary>
        public Guid LayoutId { get; set; }
        /// <summary>
        /// Gets or sets parent id.
        /// </summary>
        public Guid? ParentId { get; set; }
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = null!;

        private Dictionary<string, string> _metadata = [];
        /// <summary>
        /// Gets or sets structured metadata attached to the resource.
        /// </summary>
        public Dictionary<string, string> Metadata
        {
            get => _metadata;
            set => _metadata = value ?? [];
        }
    }
}
