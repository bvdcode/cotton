// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.LocalChanges;

namespace Cotton.Sync.App.Tests.LocalChanges;

public sealed class LocalSyncRootChangeFilterTests
{
    [Test]
    public void ShouldPublish_AllowsNormalFileUnderSyncRoot()
    {
        string root = CreateRootPath();
        var filter = new LocalSyncRootChangeFilter(root);

        bool shouldPublish = filter.ShouldPublish(Path.Combine(root, "Documents", "report.txt"));

        Assert.That(shouldPublish, Is.True);
    }

    [Test]
    public void ShouldPublish_IgnoresSyncMetadataDirectory()
    {
        string root = CreateRootPath();
        var filter = new LocalSyncRootChangeFilter(root);

        bool shouldPublish = filter.ShouldPublish(Path.Combine(root, ".cotton-sync", "tmp", "downloaded.download"));

        Assert.That(shouldPublish, Is.False);
    }

    [Test]
    public void ShouldPublish_IgnoresTemporaryFiles()
    {
        string root = CreateRootPath();
        var filter = new LocalSyncRootChangeFilter(root);

        bool shouldPublish = filter.ShouldPublish(Path.Combine(root, "Documents", "report.tmp"));

        Assert.That(shouldPublish, Is.False);
    }

    [Test]
    public void ShouldPublish_RejectsPathOutsideSyncRoot()
    {
        string root = CreateRootPath();
        var filter = new LocalSyncRootChangeFilter(root);

        bool shouldPublish = filter.ShouldPublish(Path.Combine(Path.GetTempPath(), "outside.txt"));

        Assert.That(shouldPublish, Is.False);
    }

    private static string CreateRootPath()
    {
        return Path.Combine(Path.GetTempPath(), "cotton-sync-root", Guid.NewGuid().ToString("N"));
    }
}
