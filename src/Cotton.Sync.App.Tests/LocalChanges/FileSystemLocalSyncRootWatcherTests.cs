// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.LocalChanges;

namespace Cotton.Sync.App.Tests.LocalChanges;

public sealed class FileSystemLocalSyncRootWatcherTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "cotton-local-watcher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public async Task StartAsync_RejectsMissingRoot()
    {
        string missingRoot = Path.Combine(_root, "missing");
        var watcher = new FileSystemLocalSyncRootWatcher(Guid.NewGuid(), missingRoot);

        DirectoryNotFoundException? exception = Assert.ThrowsAsync<DirectoryNotFoundException>(() => watcher.StartAsync());

        Assert.That(exception, Is.Not.Null);
        await watcher.DisposeAsync();
    }

    [Test]
    public async Task StartAsync_PublishesFileEvents()
    {
        Guid syncPairId = Guid.NewGuid();
        var watcher = new FileSystemLocalSyncRootWatcher(syncPairId, _root);
        var observed = new TaskCompletionSource<LocalSyncRootChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.Changed += (_, change) => observed.TrySetResult(change);

        await watcher.StartAsync();
        string changedPath = Path.Combine(_root, "file.txt");
        File.WriteAllText(changedPath, "content");

        LocalSyncRootChange localChange = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await watcher.DisposeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(localChange.SyncPairId, Is.EqualTo(syncPairId));
            Assert.That(localChange.FullPath, Is.EqualTo(changedPath));
            Assert.That(localChange.Kind, Is.AnyOf(LocalSyncRootChangeKind.Created, LocalSyncRootChangeKind.Changed));
        });
    }

    [Test]
    public async Task StartAsync_IgnoresCottonTemporaryDownloadEvents()
    {
        Guid syncPairId = Guid.NewGuid();
        var watcher = new FileSystemLocalSyncRootWatcher(syncPairId, _root);
        var observed = new TaskCompletionSource<LocalSyncRootChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.Changed += (_, change) => observed.TrySetResult(change);

        await watcher.StartAsync();
        string temporaryDirectory = Path.Combine(_root, ".cotton-sync", "tmp");
        Directory.CreateDirectory(temporaryDirectory);
        File.WriteAllText(Path.Combine(temporaryDirectory, "download.download"), "partial");

        await Task.Delay(TimeSpan.FromMilliseconds(350));
        await watcher.DisposeAsync();

        Assert.That(observed.Task.IsCompleted, Is.False);
    }
}
