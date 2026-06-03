// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Sync;

namespace Cotton.Sync.Remote;

/// <summary>
/// Represents a normalized remote change-feed item that sync code can reason about without inspecting DTO enums.
/// </summary>
public sealed class RemoteChangeImpact
{
    private RemoteChangeImpact(
        SyncChangeDto change,
        RemoteChangeTargetKind targetKind,
        RemoteChangeAction action)
    {
        Cursor = change.Cursor;
        Kind = change.Kind;
        TargetKind = targetKind;
        Action = action;
        LayoutId = change.LayoutId;
        NodeId = change.NodeId;
        NodeFileId = change.NodeFileId;
        ParentNodeId = change.ParentNodeId;
        PreviousParentNodeId = change.PreviousParentNodeId;
        FileManifestId = change.FileManifestId;
        OriginalNodeFileId = change.OriginalNodeFileId;
        Name = change.Name;
        ContentHash = change.ContentHash;
        ETag = change.ETag;
        SizeBytes = change.SizeBytes;
        CreatedAtUtc = DateTime.SpecifyKind(change.CreatedAt, DateTimeKind.Utc);
    }

    /// <summary>
    /// Gets the monotonic server cursor for this change.
    /// </summary>
    public long Cursor { get; }

    /// <summary>
    /// Gets the original wire kind.
    /// </summary>
    public SyncChangeKindDto Kind { get; }

    /// <summary>
    /// Gets the normalized target kind.
    /// </summary>
    public RemoteChangeTargetKind TargetKind { get; }

    /// <summary>
    /// Gets the normalized action.
    /// </summary>
    public RemoteChangeAction Action { get; }

    /// <summary>
    /// Gets the layout tree identifier when supplied by the server.
    /// </summary>
    public Guid? LayoutId { get; }

    /// <summary>
    /// Gets the changed folder identifier, or parent folder identifier for file events when supplied by the server.
    /// </summary>
    public Guid? NodeId { get; }

    /// <summary>
    /// Gets the changed file entry identifier when this change targets a file.
    /// </summary>
    public Guid? NodeFileId { get; }

    /// <summary>
    /// Gets the current parent folder identifier when supplied by the server.
    /// </summary>
    public Guid? ParentNodeId { get; }

    /// <summary>
    /// Gets the previous parent folder identifier for move/delete events when supplied by the server.
    /// </summary>
    public Guid? PreviousParentNodeId { get; }

    /// <summary>
    /// Gets the current immutable file manifest identifier for file mutations when supplied by the server.
    /// </summary>
    public Guid? FileManifestId { get; }

    /// <summary>
    /// Gets the original file lineage identifier for file mutations when supplied by the server.
    /// </summary>
    public Guid? OriginalNodeFileId { get; }

    /// <summary>
    /// Gets the current display name when supplied by the server.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the current lowercase hexadecimal full-content hash for file mutations when supplied by the server.
    /// </summary>
    public string? ContentHash { get; }

    /// <summary>
    /// Gets the current strong content ETag for file mutations when supplied by the server.
    /// </summary>
    public string? ETag { get; }

    /// <summary>
    /// Gets the current content size in bytes for file mutations when supplied by the server.
    /// </summary>
    public long? SizeBytes { get; }

    /// <summary>
    /// Gets the UTC creation timestamp for the remote mutation.
    /// </summary>
    public DateTime CreatedAtUtc { get; }

    /// <summary>
    /// Creates a normalized impact from a sync change DTO.
    /// </summary>
    public static RemoteChangeImpact FromDto(SyncChangeDto change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Cursor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(change), change.Cursor, "Change cursor cannot be negative.");
        }

        if (change.SizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(change), change.SizeBytes, "Change size cannot be negative.");
        }

        (RemoteChangeTargetKind targetKind, RemoteChangeAction action) = MapKind(change.Kind);
        return new RemoteChangeImpact(change, targetKind, action);
    }

    internal IEnumerable<Guid> EnumerateAffectedNodeIds()
    {
        if (NodeId.HasValue)
        {
            yield return NodeId.Value;
        }

        if (ParentNodeId.HasValue)
        {
            yield return ParentNodeId.Value;
        }

        if (PreviousParentNodeId.HasValue)
        {
            yield return PreviousParentNodeId.Value;
        }
    }

    internal IEnumerable<Guid> EnumerateAffectedNodeFileIds()
    {
        if (NodeFileId.HasValue)
        {
            yield return NodeFileId.Value;
        }

        if (OriginalNodeFileId.HasValue)
        {
            yield return OriginalNodeFileId.Value;
        }
    }

    private static (RemoteChangeTargetKind TargetKind, RemoteChangeAction Action) MapKind(SyncChangeKindDto kind)
    {
        return kind switch
        {
            SyncChangeKindDto.FileCreated => (RemoteChangeTargetKind.File, RemoteChangeAction.Created),
            SyncChangeKindDto.FileContentUpdated => (RemoteChangeTargetKind.File, RemoteChangeAction.ContentUpdated),
            SyncChangeKindDto.FileRenamed => (RemoteChangeTargetKind.File, RemoteChangeAction.Renamed),
            SyncChangeKindDto.FileMoved => (RemoteChangeTargetKind.File, RemoteChangeAction.Moved),
            SyncChangeKindDto.FileDeleted => (RemoteChangeTargetKind.File, RemoteChangeAction.Deleted),
            SyncChangeKindDto.FileRestored => (RemoteChangeTargetKind.File, RemoteChangeAction.Restored),
            SyncChangeKindDto.FolderCreated => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Created),
            SyncChangeKindDto.FolderRenamed => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Renamed),
            SyncChangeKindDto.FolderMoved => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Moved),
            SyncChangeKindDto.FolderDeleted => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Deleted),
            SyncChangeKindDto.FolderRestored => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Restored),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported remote change kind."),
        };
    }
}
