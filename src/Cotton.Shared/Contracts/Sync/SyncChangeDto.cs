// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Common;
using Cotton.Models.Enums;

namespace Cotton.Contracts.Sync
{
    /// <summary>
    /// Represents one durable ordered file-tree mutation.
    /// </summary>
    public class SyncChangeDto : BaseApiDto<long>
    {
        /// <summary>Mutation kind.</summary>
        public SyncChangeKind Kind { get; set; }

        /// <summary>Layout tree that contains the changed entity.</summary>
        public Guid LayoutId { get; set; }

        /// <summary>Changed file or folder identifier. The mutation kind determines the entity type.</summary>
        public Guid ItemId { get; set; }

        /// <summary>Parent folder identifier after the mutation, or before deletion.</summary>
        public Guid ParentNodeId { get; set; }

        /// <summary>Previous parent folder identifier for move events.</summary>
        public Guid? PreviousParentNodeId { get; set; }

        /// <summary>Immutable file manifest identifier for file content-bearing mutations.</summary>
        public Guid? FileManifestId { get; set; }

        /// <summary>Display name captured at the time of the mutation.</summary>
        public string Name { get; set; } = string.Empty;
    }
}
