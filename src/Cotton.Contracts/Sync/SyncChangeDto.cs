// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Contracts.Sync;

/// <summary>
/// Represents one durable ordered file-tree mutation.
/// </summary>
public sealed class SyncChangeDto
{
    /// <summary>Monotonic server cursor used by clients to resume remote change reads.</summary>
    public long Cursor { get; set; }

    /// <summary>Mutation kind.</summary>
    public SyncChangeKindDto Kind { get; set; }

    /// <summary>Layout tree that contains the changed entity when known.</summary>
    public Guid? LayoutId { get; set; }

    /// <summary>Changed folder identifier, or parent folder identifier for a file when appropriate.</summary>
    public Guid? NodeId { get; set; }

    /// <summary>Changed file entry identifier when the mutation targets a file.</summary>
    public Guid? NodeFileId { get; set; }

    /// <summary>Current parent folder identifier when available.</summary>
    public Guid? ParentNodeId { get; set; }

    /// <summary>Previous parent folder identifier for move/delete events when available.</summary>
    public Guid? PreviousParentNodeId { get; set; }

    /// <summary>Current immutable file manifest identifier for file mutations when available.</summary>
    public Guid? FileManifestId { get; set; }

    /// <summary>Original file lineage identifier for file mutations when available.</summary>
    public Guid? OriginalNodeFileId { get; set; }

    /// <summary>Current display name when available.</summary>
    public string? Name { get; set; }

    /// <summary>Current lowercase hexadecimal full-content hash for file mutations when available.</summary>
    public string? ContentHash { get; set; }

    /// <summary>Current strong content ETag for file mutations when available.</summary>
    public string? ETag { get; set; }

    /// <summary>Current content size in bytes for file mutations when available.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>UTC change creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }
}
