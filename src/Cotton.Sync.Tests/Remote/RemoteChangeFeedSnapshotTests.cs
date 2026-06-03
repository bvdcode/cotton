// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Sync;
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
        Guid originalFileId = Guid.NewGuid();
        var changes = new[]
        {
            new SyncChangeDto
            {
                Cursor = 11,
                Kind = SyncChangeKindDto.FileContentUpdated,
                NodeId = parentId,
                NodeFileId = fileId,
                OriginalNodeFileId = originalFileId,
                ParentNodeId = parentId,
                FileManifestId = Guid.NewGuid(),
                Name = "report.txt",
                ContentHash = "abc",
                ETag = "etag-1",
                SizeBytes = 128,
                CreatedAt = DateTime.UtcNow,
            },
            new SyncChangeDto
            {
                Cursor = 12,
                Kind = SyncChangeKindDto.FolderDeleted,
                NodeId = folderId,
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
            Assert.That(snapshot.AffectedNodeFileIds, Is.EquivalentTo(new[] { fileId, originalFileId }));
        });
    }

    [TestCase(SyncChangeKindDto.FileCreated, RemoteChangeTargetKind.File, RemoteChangeAction.Created)]
    [TestCase(SyncChangeKindDto.FileContentUpdated, RemoteChangeTargetKind.File, RemoteChangeAction.ContentUpdated)]
    [TestCase(SyncChangeKindDto.FileRenamed, RemoteChangeTargetKind.File, RemoteChangeAction.Renamed)]
    [TestCase(SyncChangeKindDto.FileMoved, RemoteChangeTargetKind.File, RemoteChangeAction.Moved)]
    [TestCase(SyncChangeKindDto.FileDeleted, RemoteChangeTargetKind.File, RemoteChangeAction.Deleted)]
    [TestCase(SyncChangeKindDto.FileRestored, RemoteChangeTargetKind.File, RemoteChangeAction.Restored)]
    [TestCase(SyncChangeKindDto.FolderCreated, RemoteChangeTargetKind.Folder, RemoteChangeAction.Created)]
    [TestCase(SyncChangeKindDto.FolderRenamed, RemoteChangeTargetKind.Folder, RemoteChangeAction.Renamed)]
    [TestCase(SyncChangeKindDto.FolderMoved, RemoteChangeTargetKind.Folder, RemoteChangeAction.Moved)]
    [TestCase(SyncChangeKindDto.FolderDeleted, RemoteChangeTargetKind.Folder, RemoteChangeAction.Deleted)]
    [TestCase(SyncChangeKindDto.FolderRestored, RemoteChangeTargetKind.Folder, RemoteChangeAction.Restored)]
    public void FromDto_MapsWireKindToNormalizedImpact(
        SyncChangeKindDto kind,
        RemoteChangeTargetKind targetKind,
        RemoteChangeAction action)
    {
        var change = new SyncChangeDto
        {
            Cursor = 1,
            Kind = kind,
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
                Cursor = -1,
                Kind = SyncChangeKindDto.FileCreated,
            },
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => RemoteChangeFeedSnapshot.FromChanges(changes));
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
