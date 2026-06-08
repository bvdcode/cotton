// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;
using System;
using System.Collections.Generic;

namespace Cotton.Nodes
{
    /// <summary>
    /// Represents a folder-like node inside a Cotton layout.
    /// </summary>
    public class NodeDto : BaseDto<Guid>
    {
        private Dictionary<string, string>? _metadata;

        /// <summary>
        /// Gets or sets the layout identifier.
        /// </summary>
        public Guid LayoutId { get; set; }

        /// <summary>
        /// Gets or sets the parent node identifier, or null for the root node.
        /// </summary>
        public Guid? ParentId { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets structured metadata attached to the node.
        /// </summary>
        public Dictionary<string, string> Metadata
        {
            get => _metadata ??= [];
            set => _metadata = value;
        }
    }
}
