// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Shared.Contracts.Sync;

namespace Cotton.Sync.Remote;

/// <summary>
/// Summarizes a remote change-feed page for checkpointing, diagnostics, and future incremental invalidation.
/// </summary>
public sealed class RemoteChangeFeedSnapshot
{
    private RemoteChangeFeedSnapshot(IReadOnlyCollection<RemoteChangeImpact> changes)
    {
        Changes = changes.ToArray();
        IsEmpty = Changes.Count == 0;
        FirstCursor = IsEmpty ? null : Changes[0].Cursor;
        LastCursor = IsEmpty ? null : Changes[^1].Cursor;
        ContainsFileChanges = Changes.Any(change => change.TargetKind == RemoteChangeTargetKind.File);
        ContainsFolderChanges = Changes.Any(change => change.TargetKind == RemoteChangeTargetKind.Folder);
        ContainsContentChanges = Changes.Any(change => change.Action == RemoteChangeAction.ContentUpdated);
        ContainsDeletes = Changes.Any(change => change.Action == RemoteChangeAction.Deleted);
        ContainsRestores = Changes.Any(change => change.Action == RemoteChangeAction.Restored);
        ContainsMovesOrRenames = Changes.Any(change =>
            change.Action is RemoteChangeAction.Moved or RemoteChangeAction.Renamed);
        RequiresRemoteTreeRefresh = !IsEmpty;
        AffectedNodeIds = Changes
            .SelectMany(change => change.EnumerateAffectedNodeIds())
            .Distinct()
            .ToArray();
        AffectedNodeFileIds = Changes
            .SelectMany(change => change.EnumerateAffectedNodeFileIds())
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Gets a shared empty snapshot.
    /// </summary>
    public static RemoteChangeFeedSnapshot Empty { get; } = new(Array.Empty<RemoteChangeImpact>());

    /// <summary>
    /// Gets normalized remote changes in server feed order.
    /// </summary>
    public IReadOnlyList<RemoteChangeImpact> Changes { get; }

    /// <summary>
    /// Gets a value indicating whether this snapshot contains no changes.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>
    /// Gets the first cursor in this snapshot, when any change exists.
    /// </summary>
    public long? FirstCursor { get; }

    /// <summary>
    /// Gets the last cursor in this snapshot, when any change exists.
    /// </summary>
    public long? LastCursor { get; }

    /// <summary>
    /// Gets a value indicating whether this snapshot contains at least one file mutation.
    /// </summary>
    public bool ContainsFileChanges { get; }

    /// <summary>
    /// Gets a value indicating whether this snapshot contains at least one folder mutation.
    /// </summary>
    public bool ContainsFolderChanges { get; }

    /// <summary>
    /// Gets a value indicating whether this snapshot contains at least one file content mutation.
    /// </summary>
    public bool ContainsContentChanges { get; }

    /// <summary>
    /// Gets a value indicating whether this snapshot contains at least one delete mutation.
    /// </summary>
    public bool ContainsDeletes { get; }

    /// <summary>
    /// Gets a value indicating whether this snapshot contains at least one restore mutation.
    /// </summary>
    public bool ContainsRestores { get; }

    /// <summary>
    /// Gets a value indicating whether this snapshot contains at least one move or rename mutation.
    /// </summary>
    public bool ContainsMovesOrRenames { get; }

    /// <summary>
    /// Gets a value indicating whether the local cached remote tree should be refreshed before decisions are made.
    /// </summary>
    public bool RequiresRemoteTreeRefresh { get; }

    /// <summary>
    /// Gets folder node identifiers affected by the changes.
    /// </summary>
    public IReadOnlyList<Guid> AffectedNodeIds { get; }

    /// <summary>
    /// Gets file entry identifiers affected by the changes.
    /// </summary>
    public IReadOnlyList<Guid> AffectedNodeFileIds { get; }

    /// <summary>
    /// Creates a snapshot from sync change DTOs.
    /// </summary>
    public static RemoteChangeFeedSnapshot FromChanges(IReadOnlyCollection<SyncChangeDto> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
        {
            return Empty;
        }

        return new RemoteChangeFeedSnapshot(changes.Select(RemoteChangeImpact.FromDto).ToArray());
    }
}
