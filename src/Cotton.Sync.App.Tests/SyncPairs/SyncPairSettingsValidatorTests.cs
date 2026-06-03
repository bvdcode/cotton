// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.Tests.SyncPairs;

public sealed class SyncPairSettingsValidatorTests
{
    private readonly SyncPairSettingsValidator _validator = new();

    [Test]
    public void Validate_AcceptsFullMirrorSyncPair()
    {
        SyncPairValidationResult result = _validator.Validate([CreatePair("/home/user/Cotton")]);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void Validate_RejectsMissingRequiredValues()
    {
        var syncPair = new SyncPairSettings
        {
            Id = Guid.Empty,
            DisplayName = " ",
            LocalRootPath = string.Empty,
            RemoteRootNodeId = Guid.Empty,
            RemoteDisplayPath = " ",
        };

        SyncPairValidationResult result = _validator.Validate([syncPair]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Select(error => error.Issue), Is.EquivalentTo(new[]
            {
                SyncPairValidationIssue.EmptyId,
                SyncPairValidationIssue.EmptyDisplayName,
                SyncPairValidationIssue.EmptyLocalRootPath,
                SyncPairValidationIssue.EmptyRemoteRootNodeId,
                SyncPairValidationIssue.EmptyRemoteDisplayPath,
            }));
        });
    }

    [Test]
    public void Validate_RejectsVirtualFilesPlaceholderMode()
    {
        SyncPairSettings syncPair = CreatePair("/home/user/Cotton");
        syncPair.Mode = SyncPairMode.VirtualFilesPlaceholder;

        SyncPairValidationResult result = _validator.Validate([syncPair]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Single().Issue, Is.EqualTo(SyncPairValidationIssue.UnsupportedMode));
        });
    }

    [Test]
    public void Validate_RejectsNestedWindowsRootsIgnoringCase()
    {
        SyncPairSettings first = CreatePair(@"C:\Users\Vadim\Cotton");
        SyncPairSettings second = CreatePair(@"c:/users/vadim/cotton/Work");

        SyncPairValidationResult result = _validator.Validate([first, second]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Single().Issue, Is.EqualTo(SyncPairValidationIssue.OverlappingLocalRoots));
            Assert.That(result.Errors.Single().SyncPairId, Is.EqualTo(first.Id));
            Assert.That(result.Errors.Single().OtherSyncPairId, Is.EqualTo(second.Id));
        });
    }

    [Test]
    public void Validate_RejectsEqualWindowsRootsIgnoringTrailingSeparators()
    {
        SyncPairSettings first = CreatePair(@"D:\Sync\Cotton\");
        SyncPairSettings second = CreatePair(@"d:/sync/cotton");

        SyncPairValidationResult result = _validator.Validate([first, second]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Single().Issue, Is.EqualTo(SyncPairValidationIssue.OverlappingLocalRoots));
        });
    }

    [Test]
    public void Validate_RejectsNestedUnixRoots()
    {
        SyncPairSettings first = CreatePair("/home/user/cotton");
        SyncPairSettings second = CreatePair("/home/user/cotton/work");

        SyncPairValidationResult result = _validator.Validate([first, second]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Single().Issue, Is.EqualTo(SyncPairValidationIssue.OverlappingLocalRoots));
        });
    }

    [Test]
    public void Validate_AllowsUnixRootsThatDifferOnlyByCase()
    {
        SyncPairSettings first = CreatePair("/home/user/Cotton");
        SyncPairSettings second = CreatePair("/home/user/cotton");

        SyncPairValidationResult result = _validator.Validate([first, second]);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_AllowsSiblingRootsWithSharedPrefix()
    {
        SyncPairSettings first = CreatePair("/home/user/cotton");
        SyncPairSettings second = CreatePair("/home/user/cotton-backup");

        SyncPairValidationResult result = _validator.Validate([first, second]);

        Assert.That(result.IsValid, Is.True);
    }

    private static SyncPairSettings CreatePair(string localRootPath)
    {
        return new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = "Documents",
            LocalRootPath = localRootPath,
            RemoteRootNodeId = Guid.NewGuid(),
            RemoteDisplayPath = "/Documents",
            Mode = SyncPairMode.FullMirror,
        };
    }
}
