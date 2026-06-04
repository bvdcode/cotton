// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents one durable sync-change API payload.
    /// </summary>
    public class SyncChangeDto
    {
        /// <summary>
        /// Gets or sets the cursor assigned to this change.
        /// </summary>
        public long Cursor { get; set; }
        /// <summary>
        /// Gets or sets the mutation kind.
        /// </summary>
        public SyncChangeKind Kind { get; set; }
        /// <summary>
        /// Gets or sets the layout identifier.
        /// </summary>
        public Guid LayoutId { get; set; }
        /// <summary>
        /// Gets or sets the changed file or folder identifier.
        /// </summary>
        public Guid ItemId { get; set; }
        /// <summary>
        /// Gets or sets the parent folder identifier after the mutation, or before deletion.
        /// </summary>
        public Guid ParentNodeId { get; set; }
        /// <summary>
        /// Gets or sets the previous parent folder identifier for move events.
        /// </summary>
        public Guid? PreviousParentNodeId { get; set; }
        /// <summary>
        /// Gets or sets the immutable file manifest identifier for file content-bearing mutations.
        /// </summary>
        public Guid? FileManifestId { get; set; }
        /// <summary>
        /// Gets or sets the display name captured at the time of the mutation.
        /// </summary>
        public string Name { get; set; } = null!;
        /// <summary>
        /// Gets or sets the time when the change was recorded.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
