// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;
using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;

namespace Cotton.Sync.Tests;

public sealed class SyncEngineTests
{
    private readonly Guid _remoteRootNodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private string _root = string.Empty;
    private string _databasePath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "cotton-sync-engine", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _databasePath = Path.Combine(_root, ".cotton-sync", "state.sqlite");
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
    public async Task RunOnceAsync_UploadsLocalOnlyFileAndStoresBaseline()
    {
        LocalFileSnapshot local = LocalFile("Docs/local.txt", "local-content");
        var scanner = new FakeLocalFileScanner(local);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        var progress = new List<SyncActivity>();
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), remoteFiles, out SqliteSyncStateStore stateStore);

        SyncRunResult result = await engine.RunOnceAsync(
            Pair(),
            new SyncRunOptions { ActivityProgress = new Progress<SyncActivity>(progress.Add) });

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", "docs/LOCAL.txt");
        Assert.Multiple(() =>
        {
            Assert.That(remoteFiles.Uploads, Has.Count.EqualTo(1));
            Assert.That(remoteFiles.Uploads[0].RelativePath, Is.EqualTo("Docs/local.txt"));
            Assert.That(remoteFiles.Uploads[0].ExistingRemoteFile, Is.Null);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(progress.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(local.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(local.ContentHash));
            Assert.That(entry.RemoteFileId, Is.EqualTo(remoteFiles.Uploads[0].ReturnedFile.Id));
        });
    }

    [Test]
    public async Task RunOnceAsync_DownloadsRemoteOnlyFileAndStoresBaseline()
    {
        byte[] content = Encoding.UTF8.GetBytes("remote-content");
        NodeFileManifestDto remote = RemoteFile("remote.txt", Hash(content));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.Downloads[remote.Id] = content;
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", "remote.txt");
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(_root, "remote.txt")), Is.EqualTo("remote-content"));
            Assert.That(remoteFiles.Deletes, Is.Empty);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(remote.ContentHash));
            Assert.That(entry.RemoteFileId, Is.EqualTo(remote.Id));
        });
    }

    [Test]
    public async Task RunOnceAsync_UploadsLocalChangeWhenRemoteBaselineIsUnchanged()
    {
        LocalFileSnapshot local = LocalFile("changed.txt", "local-new");
        NodeFileManifestDto remote = RemoteFile("changed.txt", HashText("old"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(local), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, "changed.txt", HashText("old"), remote);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", "changed.txt");
        Assert.Multiple(() =>
        {
            Assert.That(remoteFiles.Uploads, Has.Count.EqualTo(1));
            Assert.That(remoteFiles.Uploads[0].ExistingRemoteFile!.Id, Is.EqualTo(remote.Id));
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(entry!.LocalContentHash, Is.EqualTo(local.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(local.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_DownloadsRemoteChangeWhenLocalBaselineIsUnchanged()
    {
        string relativePath = "changed-down.txt";
        WriteFile(relativePath, "old");
        LocalFileSnapshot local = LocalFile(relativePath, "old");
        byte[] remoteContent = Encoding.UTF8.GetBytes("remote-new");
        NodeFileManifestDto remote = RemoteFile(relativePath, Hash(remoteContent));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.Downloads[remote.Id] = remoteContent;
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(local), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, relativePath, local.ContentHash, RemoteFile(relativePath, local.ContentHash, remote.Id));

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(_root, relativePath)), Is.EqualTo("remote-new"));
            Assert.That(remoteFiles.Uploads, Is.Empty);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(entry!.LocalContentHash, Is.EqualTo(remote.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(remote.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_DeletesRemoteOnlyWhenBaselineKnowsLocalDelete()
    {
        NodeFileManifestDto remote = RemoteFile("delete-remote.txt", HashText("old"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, "delete-remote.txt", remote.ContentHash, remote);

        SyncRunResult result = await engine.RunOnceAsync(Pair(), new SyncRunOptions { DeleteRemotePermanently = true });

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", "delete-remote.txt");
        Assert.Multiple(() =>
        {
            Assert.That(remoteFiles.Deletes, Is.EqualTo(new[] { (remote.Id, true, remote.ETag) }));
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedRemote }));
            Assert.That(entry, Is.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_BlocksRemoteDeletesOverRunLimit()
    {
        NodeFileManifestDto firstRemote = RemoteFile("a.txt", HashText("old-a"));
        NodeFileManifestDto secondRemote = RemoteFile("b.txt", HashText("old-b"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(),
            RemoteTree(firstRemote, secondRemote),
            remoteFiles,
            out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, "a.txt", firstRemote.ContentHash, firstRemote);
        await InsertBaselineAsync(stateStore, "b.txt", secondRemote.ContentHash, secondRemote);

        SyncRunResult result = await engine.RunOnceAsync(Pair(), new SyncRunOptions { MaximumRemoteDeletesPerRun = 1 });

        SyncStateEntry? firstEntry = await stateStore.GetAsync("pair-a", "a.txt");
        SyncStateEntry? secondEntry = await stateStore.GetAsync("pair-a", "b.txt");
        Assert.Multiple(() =>
        {
            Assert.That(remoteFiles.Deletes, Is.EqualTo(new[] { (firstRemote.Id, false, firstRemote.ETag) }));
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[]
            {
                SyncActivityKind.DeletedRemote,
                SyncActivityKind.Skipped,
            }));
            Assert.That(result.Activities[1].Details, Does.Contain("mass-delete guard"));
            Assert.That(firstEntry, Is.Null);
            Assert.That(secondEntry, Is.Not.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_DownloadsRemoteFileInsteadOfDeletingWhenBaselineIsMissing()
    {
        byte[] content = Encoding.UTF8.GetBytes("no-baseline-remote");
        NodeFileManifestDto remote = RemoteFile("safe-download.txt", Hash(content));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.Downloads[remote.Id] = content;
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), RemoteTree(remote), remoteFiles, out _);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        Assert.Multiple(() =>
        {
            Assert.That(remoteFiles.Deletes, Is.Empty);
            Assert.That(File.ReadAllText(Path.Combine(_root, "safe-download.txt")), Is.EqualTo("no-baseline-remote"));
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
        });
    }

    [Test]
    public async Task RunOnceAsync_DeletesLocalWhenBaselineKnowsRemoteDelete()
    {
        string relativePath = "delete-local.txt";
        WriteFile(relativePath, "old");
        LocalFileSnapshot local = LocalFile(relativePath, "old");
        NodeFileManifestDto baselineRemote = RemoteFile(relativePath, local.ContentHash);
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(local), EmptyRemoteTree(), new FakeRemoteFileSynchronizer(), out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, relativePath, local.ContentHash, baselineRemote);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(_root, relativePath)), Is.False);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedLocal }));
            Assert.That(entry, Is.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_BlocksLocalDeletesOverRunLimit()
    {
        WriteFile("a.txt", "old-a");
        WriteFile("b.txt", "old-b");
        LocalFileSnapshot firstLocal = LocalFile("a.txt", "old-a");
        LocalFileSnapshot secondLocal = LocalFile("b.txt", "old-b");
        NodeFileManifestDto firstRemote = RemoteFile("a.txt", firstLocal.ContentHash);
        NodeFileManifestDto secondRemote = RemoteFile("b.txt", secondLocal.ContentHash);
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(firstLocal, secondLocal),
            EmptyRemoteTree(),
            new FakeRemoteFileSynchronizer(),
            out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, "a.txt", firstLocal.ContentHash, firstRemote);
        await InsertBaselineAsync(stateStore, "b.txt", secondLocal.ContentHash, secondRemote);

        SyncRunResult result = await engine.RunOnceAsync(Pair(), new SyncRunOptions { MaximumLocalDeletesPerRun = 1 });

        SyncStateEntry? firstEntry = await stateStore.GetAsync("pair-a", "a.txt");
        SyncStateEntry? secondEntry = await stateStore.GetAsync("pair-a", "b.txt");
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(_root, "a.txt")), Is.False);
            Assert.That(File.Exists(Path.Combine(_root, "b.txt")), Is.True);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[]
            {
                SyncActivityKind.DeletedLocal,
                SyncActivityKind.Skipped,
            }));
            Assert.That(result.Activities[1].Details, Does.Contain("mass-delete guard"));
            Assert.That(firstEntry, Is.Null);
            Assert.That(secondEntry, Is.Not.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_PreservesBothVersionsWhenLocalAndRemoteChanged()
    {
        string relativePath = "conflict.txt";
        WriteFile(relativePath, "local-new");
        LocalFileSnapshot local = LocalFile(relativePath, "local-new");
        byte[] remoteContent = Encoding.UTF8.GetBytes("remote-new");
        NodeFileManifestDto remote = RemoteFile(relativePath, Hash(remoteContent));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.Downloads[remote.Id] = remoteContent;
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(local), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, relativePath, HashText("old"), RemoteFile(relativePath, HashText("old"), remote.Id));

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        string[] conflictFiles = Directory.GetFiles(_root, "*Cotton conflict*", SearchOption.AllDirectories);
        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(_root, relativePath)), Is.EqualTo("local-new"));
            Assert.That(conflictFiles, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(conflictFiles[0]), Is.EqualTo("remote-new"));
            Assert.That(remoteFiles.Uploads, Is.Empty);
            Assert.That(remoteFiles.Deletes, Is.Empty);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Conflict }));
            Assert.That(result.Activities[0].Details, Does.Contain("Cotton conflict"));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(local.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(remote.ContentHash));
            Assert.That(entry.LocalContentHash, Is.Not.EqualTo(entry.RemoteContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_DoesNotDuplicateConflictCopiesWhenUnresolvedConflictIsUnchanged()
    {
        string relativePath = "conflict-stable.txt";
        WriteFile(relativePath, "local-new");
        LocalFileSnapshot local = LocalFile(relativePath, "local-new");
        NodeFileManifestDto remote = RemoteFile(relativePath, HashText("remote-new"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(local), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, relativePath, local.ContentHash, remote);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        string[] conflictFiles = Directory.GetFiles(_root, "*Cotton conflict*", SearchOption.AllDirectories);
        Assert.Multiple(() =>
        {
            Assert.That(result.Activities, Is.Empty);
            Assert.That(conflictFiles, Is.Empty);
            Assert.That(remoteFiles.Uploads, Is.Empty);
            Assert.That(remoteFiles.Deletes, Is.Empty);
            Assert.That(File.ReadAllText(Path.Combine(_root, relativePath)), Is.EqualTo("local-new"));
        });
    }

    [Test]
    public async Task RunOnceAsync_PreservesUnresolvedConflictWhenRemoteChangesAgain()
    {
        string relativePath = "conflict-remote-again.txt";
        WriteFile(relativePath, "local-new");
        LocalFileSnapshot local = LocalFile(relativePath, "local-new");
        byte[] remoteContent = Encoding.UTF8.GetBytes("remote-newer");
        NodeFileManifestDto remote = RemoteFile(relativePath, Hash(remoteContent));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.Downloads[remote.Id] = remoteContent;
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(local), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, relativePath, local.ContentHash, RemoteFile(relativePath, HashText("remote-old"), remote.Id));

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        string[] conflictFiles = Directory.GetFiles(_root, "*Cotton conflict*", SearchOption.AllDirectories);
        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(_root, relativePath)), Is.EqualTo("local-new"));
            Assert.That(conflictFiles, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(conflictFiles[0]), Is.EqualTo("remote-newer"));
            Assert.That(remoteFiles.Uploads, Is.Empty);
            Assert.That(remoteFiles.Deletes, Is.Empty);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Conflict }));
            Assert.That(entry!.LocalContentHash, Is.EqualTo(local.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(remote.ContentHash));
        });
    }

    [Test]
    public void RunOnceAsync_HonorsCancellationBeforeScanning()
    {
        var scanner = new FakeLocalFileScanner(LocalFile("cancel.txt", "cancel"));
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), new FakeRemoteFileSynchronizer(), out _);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => engine.RunOnceAsync(Pair(), cancellationToken: cancellation.Token));
        Assert.That(scanner.ScanCalls, Is.Zero);
    }

    private SyncEngine CreateEngine(
        FakeLocalFileScanner scanner,
        RemoteTreeSnapshot remoteTree,
        FakeRemoteFileSynchronizer remoteFiles,
        out SqliteSyncStateStore stateStore)
    {
        stateStore = new SqliteSyncStateStore(_databasePath);
        return new SyncEngine(scanner, new FakeRemoteTreeCrawler(remoteTree), remoteFiles, stateStore);
    }

    private SyncPair Pair()
    {
        return new SyncPair
        {
            SyncPairId = "pair-a",
            LocalRootPath = _root,
            RemoteRootNodeId = _remoteRootNodeId,
        };
    }

    private async Task InsertBaselineAsync(
        SqliteSyncStateStore stateStore,
        string relativePath,
        string localContentHash,
        NodeFileManifestDto remoteFile)
    {
        await stateStore.InitializeAsync();
        await stateStore.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = relativePath,
            Kind = SyncEntryKind.File,
            LocalContentHash = localContentHash,
            LocalLastWriteUtc = new DateTime(2026, 6, 2, 13, 0, 0, DateTimeKind.Utc),
            RemoteNodeId = remoteFile.NodeId,
            RemoteFileId = remoteFile.Id,
            RemoteContentHash = remoteFile.ContentHash,
            RemoteETag = remoteFile.ETag,
            SyncedAtUtc = new DateTime(2026, 6, 2, 13, 1, 0, DateTimeKind.Utc),
        });
    }

    private LocalFileSnapshot LocalFile(string relativePath, string content)
    {
        return new LocalFileSnapshot
        {
            RelativePath = relativePath.Replace('\\', '/'),
            FullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            ContentHash = HashText(content),
            SizeBytes = Encoding.UTF8.GetByteCount(content),
            LastWriteUtc = new DateTime(2026, 6, 2, 13, 0, 0, DateTimeKind.Utc),
        };
    }

    private void WriteFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetLastWriteTimeUtc(fullPath, new DateTime(2026, 6, 2, 13, 0, 0, DateTimeKind.Utc));
    }

    private RemoteTreeSnapshot EmptyRemoteTree()
    {
        return new RemoteTreeSnapshot
        {
            RootNode = new NodeDto
            {
                Id = _remoteRootNodeId,
                Name = "root",
            },
        };
    }

    private RemoteTreeSnapshot RemoteTree(params NodeFileManifestDto[] files)
    {
        RemoteTreeSnapshot tree = EmptyRemoteTree();
        foreach (NodeFileManifestDto file in files)
        {
            tree.Files.Add(new RemoteFileSnapshot
            {
                RelativePath = file.Metadata["relativePath"],
                File = file,
            });
        }

        return tree;
    }

    private NodeFileManifestDto RemoteFile(string relativePath, string contentHash, Guid? id = null)
    {
        return new NodeFileManifestDto
        {
            Id = id ?? Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 6, 2, 12, 30, 0, DateTimeKind.Utc),
            NodeId = _remoteRootNodeId,
            FileManifestId = Guid.NewGuid(),
            OriginalNodeFileId = id ?? Guid.NewGuid(),
            OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = relativePath.Split('/')[^1],
            ContentType = "text/plain",
            SizeBytes = 1,
            ContentHash = contentHash,
            ETag = "sha256-" + contentHash,
            Metadata = new Dictionary<string, string> { ["relativePath"] = relativePath.Replace('\\', '/') },
        };
    }

    private static string HashText(string text)
    {
        return Hash(Encoding.UTF8.GetBytes(text));
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private sealed class FakeLocalFileScanner : ILocalFileScanner
    {
        public FakeLocalFileScanner(params LocalFileSnapshot[] files)
        {
            Files = files.ToList();
        }

        public List<LocalFileSnapshot> Files { get; }

        public int ScanCalls { get; private set; }

        public Task<IReadOnlyList<LocalFileSnapshot>> ScanAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            ScanCalls++;
            return Task.FromResult<IReadOnlyList<LocalFileSnapshot>>(Files);
        }
    }

    private sealed class FakeRemoteTreeCrawler : IRemoteTreeCrawler
    {
        private readonly RemoteTreeSnapshot _snapshot;

        public FakeRemoteTreeCrawler(RemoteTreeSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<RemoteTreeSnapshot> CrawlAsync(Guid rootNodeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_snapshot);
        }
    }

    private sealed class FakeRemoteFileSynchronizer : IRemoteFileSynchronizer
    {
        public List<UploadCall> Uploads { get; } = [];

        public List<(Guid NodeFileId, bool SkipTrash, string? ExpectedETag)> Deletes { get; } = [];

        public Dictionary<Guid, byte[]> Downloads { get; } = [];

        public Task<NodeFileManifestDto> UploadFileAsync(
            Guid rootNodeId,
            string relativePath,
            LocalFileSnapshot localFile,
            NodeFileManifestDto? existingRemoteFile = null,
            CancellationToken cancellationToken = default)
        {
            var returned = new NodeFileManifestDto
            {
                Id = existingRemoteFile?.Id ?? Guid.NewGuid(),
                NodeId = existingRemoteFile?.NodeId ?? rootNodeId,
                FileManifestId = Guid.NewGuid(),
                OriginalNodeFileId = existingRemoteFile?.OriginalNodeFileId == Guid.Empty
                    ? Guid.NewGuid()
                    : existingRemoteFile?.OriginalNodeFileId ?? Guid.NewGuid(),
                OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = relativePath.Split('/')[^1],
                ContentType = "application/octet-stream",
                SizeBytes = localFile.SizeBytes,
                ContentHash = localFile.ContentHash,
                ETag = "sha256-" + localFile.ContentHash,
                CreatedAt = new DateTime(2026, 6, 2, 14, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 6, 2, 14, 0, 0, DateTimeKind.Utc),
            };
            Uploads.Add(new UploadCall(rootNodeId, relativePath, localFile, existingRemoteFile, returned));
            return Task.FromResult(returned);
        }

        public Task DownloadFileAsync(Guid nodeFileId, Stream destination, CancellationToken cancellationToken = default)
        {
            byte[] bytes = Downloads[nodeFileId];
            return destination.WriteAsync(bytes, cancellationToken).AsTask();
        }

        public Task DeleteFileAsync(
            Guid nodeFileId,
            bool skipTrash = false,
            string? expectedETag = null,
            CancellationToken cancellationToken = default)
        {
            Deletes.Add((nodeFileId, skipTrash, expectedETag));
            return Task.CompletedTask;
        }
    }

    private sealed record UploadCall(
        Guid RootNodeId,
        string RelativePath,
        LocalFileSnapshot LocalFile,
        NodeFileManifestDto? ExistingRemoteFile,
        NodeFileManifestDto ReturnedFile);
}
