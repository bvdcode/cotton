// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Sync;
using Cotton.Models.Enums;
using Cotton.Sync.Remote;

namespace Cotton.Sync.Tests.Remote;

public sealed class RemoteChangeFeedSnapshotTests
{
    [Test]
    public void FromChanges_SummarizesAffectedEntitiesAndActions()
    {
        Guid folderId = Guid.NewGuid();
        Guid parentId = Guid.NewGuid();
        Guid previousParentId = Guid.NewGuid();
        Guid fileId = Guid.NewGuid();
        Guid layoutId = Guid.NewGuid();
        var changes = new[]
        {
            new SyncChangeDto
            {
                Id = 11,
                Kind = SyncChangeKind.FileContentUpdated,
                LayoutId = layoutId,
                ItemId = fileId,
                ParentNodeId = parentId,
                FileManifestId = Guid.NewGuid(),
                Name = "report.txt",
                CreatedAt = DateTime.UtcNow,
            },
            new SyncChangeDto
            {
                Id = 12,
                Kind = SyncChangeKind.FolderDeleted,
                LayoutId = layoutId,
                ItemId = folderId,
                ParentNodeId = parentId,
                PreviousParentNodeId = previousParentId,
                Name = "Archive",
                CreatedAt = DateTime.UtcNow,
            },
        };

        RemoteChangeFeedSnapshot snapshot = RemoteChangeFeedSnapshot.FromChanges(changes);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsEmpty, Is.False);
            Assert.That(snapshot.FirstCursor, Is.EqualTo(11));
            Assert.That(snapshot.LastCursor, Is.EqualTo(12));
            Assert.That(snapshot.ContainsFileChanges, Is.True);
            Assert.That(snapshot.ContainsFolderChanges, Is.True);
            Assert.That(snapshot.ContainsContentChanges, Is.True);
            Assert.That(snapshot.ContainsDeletes, Is.True);
            Assert.That(snapshot.ContainsMovesOrRenames, Is.False);
            Assert.That(snapshot.RequiresRemoteTreeRefresh, Is.True);
            Assert.That(snapshot.AffectedNodeIds, Is.EquivalentTo(new[] { parentId, folderId, previousParentId }));
            Assert.That(snapshot.AffectedNodeFileIds, Is.EquivalentTo(new[] { fileId }));
        });
    }

    [TestCase(SyncChangeKind.FileCreated, RemoteChangeTargetKind.File, RemoteChangeAction.Created)]
    [TestCase(SyncChangeKind.FileContentUpdated, RemoteChangeTargetKind.File, RemoteChangeAction.ContentUpdated)]
    [TestCase(SyncChangeKind.FileRenamed, RemoteChangeTargetKind.File, RemoteChangeAction.Renamed)]
    [TestCase(SyncChangeKind.FileMoved, RemoteChangeTargetKind.File, RemoteChangeAction.Moved)]
    [TestCase(SyncChangeKind.FileDeleted, RemoteChangeTargetKind.File, RemoteChangeAction.Deleted)]
    [TestCase(SyncChangeKind.FileRestored, RemoteChangeTargetKind.File, RemoteChangeAction.Restored)]
    [TestCase(SyncChangeKind.FolderCreated, RemoteChangeTargetKind.Folder, RemoteChangeAction.Created)]
    [TestCase(SyncChangeKind.FolderRenamed, RemoteChangeTargetKind.Folder, RemoteChangeAction.Renamed)]
    [TestCase(SyncChangeKind.FolderMoved, RemoteChangeTargetKind.Folder, RemoteChangeAction.Moved)]
    [TestCase(SyncChangeKind.FolderDeleted, RemoteChangeTargetKind.Folder, RemoteChangeAction.Deleted)]
    [TestCase(SyncChangeKind.FolderRestored, RemoteChangeTargetKind.Folder, RemoteChangeAction.Restored)]
    public void FromDto_MapsWireKindToNormalizedImpact(
        SyncChangeKind kind,
        RemoteChangeTargetKind targetKind,
        RemoteChangeAction action)
    {
        var change = new SyncChangeDto
        {
            Id = 1,
            Kind = kind,
            LayoutId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            ParentNodeId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

        RemoteChangeImpact impact = RemoteChangeImpact.FromDto(change);

        Assert.Multiple(() =>
        {
            Assert.That(impact.TargetKind, Is.EqualTo(targetKind));
            Assert.That(impact.Action, Is.EqualTo(action));
        });
    }

    [Test]
    public void FromChanges_RejectsNegativeCursor()
    {
        var changes = new[]
        {
            new SyncChangeDto
            {
                Id = -1,
                Kind = SyncChangeKind.FileCreated,
            },
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => RemoteChangeFeedSnapshot.FromChanges(changes));
    }

    [Test]
    public void FromDto_RejectsUnknownWireKind()
    {
        var change = new SyncChangeDto
        {
            Id = 1,
            Kind = SyncChangeKind.Unknown,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => RemoteChangeImpact.FromDto(change));
    }

    [Test]
    public void Empty_ReturnsReusableEmptySnapshot()
    {
        RemoteChangeFeedSnapshot snapshot = RemoteChangeFeedSnapshot.FromChanges(Array.Empty<SyncChangeDto>());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot, Is.SameAs(RemoteChangeFeedSnapshot.Empty));
            Assert.That(snapshot.IsEmpty, Is.True);
            Assert.That(snapshot.RequiresRemoteTreeRefresh, Is.False);
            Assert.That(snapshot.AffectedNodeIds, Is.Empty);
            Assert.That(snapshot.AffectedNodeFileIds, Is.Empty);
        });
    }
}
