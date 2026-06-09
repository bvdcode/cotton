// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync;
using Cotton.Models.Enums;

namespace Cotton.Sync.Remote
{

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
            Cursor = change.Id;
            Kind = change.Kind;
            TargetKind = targetKind;
            Action = action;
            LayoutId = change.LayoutId;
            NodeId = targetKind == RemoteChangeTargetKind.Folder ? change.ItemId : change.ParentNodeId;
            NodeFileId = targetKind == RemoteChangeTargetKind.File ? change.ItemId : null;
            ParentNodeId = change.ParentNodeId;
            PreviousParentNodeId = change.PreviousParentNodeId;
            FileManifestId = change.FileManifestId;
            Name = change.Name;
            CreatedAtUtc = DateTime.SpecifyKind(change.CreatedAt, DateTimeKind.Utc);
        }

        /// <summary>
        /// Gets the monotonic server cursor for this change.
        /// </summary>
        public long Cursor { get; }

        /// <summary>
        /// Gets the original wire kind.
        /// </summary>
        public SyncChangeKind Kind { get; }

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
        /// Gets the current display name captured by the server.
        /// </summary>
        public string Name { get; }

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
            if (change.Id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(change), change.Id, "Change cursor must be positive.");
            }

            if (change.LayoutId == Guid.Empty)
            {
                throw new ArgumentException("Change layout id is required.", nameof(change));
            }

            if (change.ItemId == Guid.Empty)
            {
                throw new ArgumentException("Changed item id is required.", nameof(change));
            }

            if (change.ParentNodeId == Guid.Empty)
            {
                throw new ArgumentException("Changed item parent node id is required.", nameof(change));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(change.Name);

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

        }

        private static (RemoteChangeTargetKind TargetKind, RemoteChangeAction Action) MapKind(SyncChangeKind kind)
        {
            return kind switch
            {
                SyncChangeKind.FileCreated => (RemoteChangeTargetKind.File, RemoteChangeAction.Created),
                SyncChangeKind.FileContentUpdated => (RemoteChangeTargetKind.File, RemoteChangeAction.ContentUpdated),
                SyncChangeKind.FileRenamed => (RemoteChangeTargetKind.File, RemoteChangeAction.Renamed),
                SyncChangeKind.FileMoved => (RemoteChangeTargetKind.File, RemoteChangeAction.Moved),
                SyncChangeKind.FileDeleted => (RemoteChangeTargetKind.File, RemoteChangeAction.Deleted),
                SyncChangeKind.FileRestored => (RemoteChangeTargetKind.File, RemoteChangeAction.Restored),
                SyncChangeKind.FolderCreated => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Created),
                SyncChangeKind.FolderRenamed => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Renamed),
                SyncChangeKind.FolderMoved => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Moved),
                SyncChangeKind.FolderDeleted => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Deleted),
                SyncChangeKind.FolderRestored => (RemoteChangeTargetKind.Folder, RemoteChangeAction.Restored),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported remote change kind."),
            };
        }
    }
}
