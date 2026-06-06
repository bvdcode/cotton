// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;
using Microsoft.Extensions.Logging;

namespace Cotton.Sync.Tests;

public sealed class SyncEngineTests
{
    private readonly Guid _remoteRootNodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private string _root = string.Empty;
    private string _databasePath = string.Empty;

    public enum MatrixFileState
    {
        Missing,
        Baseline,
        Changed,
    }

    [Test]
    public async Task RunOnceAsync_WritesStructuredStartAndCompletionLogs()
    {
        var logger = new RecordingLogger<SyncEngine>();
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(),
            EmptyRemoteTree(),
            new FakeRemoteFileSynchronizer(),
            out _,
            logger: logger);

        await engine.RunOnceAsync(Pair());

        Assert.Multiple(() =>
        {
            Assert.That(logger.Entries.Select(entry => entry.Level), Is.EqualTo(new[] { LogLevel.Information, LogLevel.Information }));
            Assert.That(logger.Entries[0].Message, Does.Contain("Starting sync pass for pair pair-a"));
            Assert.That(logger.Entries[1].Message, Does.Contain("Completed sync pass for pair pair-a with 0 activities"));
        });
    }

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
    public async Task RunOnceAsync_UploadsLocalOnlyMetadataSnapshotWithoutPreHashing()
    {
        const string uploadedHash = "uploaded-content-hash";
        var local = new LocalFileSnapshot
        {
            RelativePath = "Docs/large.bin",
            FullPath = Path.Combine(_root, "Docs", "large.bin"),
            ContentHash = string.Empty,
            SizeBytes = 1024,
            LastWriteUtc = new DateTime(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc),
        };
        var scanner = new MetadataOnlyLocalFileScanner(local);
        var remoteFiles = new FakeRemoteFileSynchronizer
        {
            EmptyLocalHashUploadContentHash = uploadedHash,
        };
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), remoteFiles, out SqliteSyncStateStore stateStore);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", "docs/large.bin");
        Assert.Multiple(() =>
        {
            Assert.That(scanner.ContentHashCalls, Is.Zero);
            Assert.That(remoteFiles.UploadInputContentHashes, Is.EqualTo(new[] { string.Empty }));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(uploadedHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(uploadedHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_HashesMetadataSnapshotWhenBaselineNeedsComparison()
    {
        const string baselineHash = "precomputed-content-hash";
        var local = new LocalFileSnapshot
        {
            RelativePath = "Docs/existing.bin",
            FullPath = Path.Combine(_root, "Docs", "existing.bin"),
            ContentHash = string.Empty,
            SizeBytes = 1024,
            LastWriteUtc = new DateTime(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc),
        };
        var scanner = new MetadataOnlyLocalFileScanner(local);
        NodeFileManifestDto remote = RemoteFile("Docs/existing.bin", baselineHash, sizeBytes: local.SizeBytes);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        SyncEngine engine = CreateEngine(scanner, RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, "Docs/existing.bin", baselineHash, remote);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        Assert.Multiple(() =>
        {
            Assert.That(scanner.ContentHashCalls, Is.EqualTo(1));
            Assert.That(remoteFiles.Uploads, Is.Empty);
            Assert.That(result.Activities, Is.Empty);
        });
    }

    [Test]
    public async Task RunOnceAsync_ReportsAggregateRunProgressFileCounts()
    {
        var scanner = new FakeLocalFileScanner(
            LocalFile("Docs/a.txt", "a"),
            LocalFile("Docs/b.txt", "b"));
        var progress = new RecordingProgress<SyncRunProgress>();
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), new FakeRemoteFileSynchronizer(), out _);

        await engine.RunOnceAsync(
            Pair(),
            new SyncRunOptions { RunProgress = progress });

        IReadOnlyList<SyncRunProgress> fileProgress = progress.Values
            .Where(item => item.Stage == SyncRunProgressStage.ReconcilingFiles)
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(progress.Values[0].Stage, Is.EqualTo(SyncRunProgressStage.ScanningLocal));
            Assert.That(progress.Values.Any(item => item.Stage == SyncRunProgressStage.ScanningRemote), Is.True);
            Assert.That(progress.Values.Any(item => item.Stage == SyncRunProgressStage.ReconcilingDirectories), Is.True);
            Assert.That(fileProgress.Select(item => item.FilesTotal).Distinct(), Is.EqualTo(new int?[] { 2 }));
            Assert.That(fileProgress.Select(item => item.FilesCompleted).Distinct(), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(fileProgress.Where(item => !string.IsNullOrWhiteSpace(item.CurrentPath)).Select(item => item.CurrentPath).Distinct(), Is.EqualTo(new[] { "Docs/a.txt", "Docs/b.txt" }));
            Assert.That(progress.Values[^1].Stage, Is.EqualTo(SyncRunProgressStage.Completed));
            Assert.That(progress.Values[^1].FilesCompleted, Is.EqualTo(2));
            Assert.That(progress.Values[^1].FilesTotal, Is.EqualTo(2));
            Assert.That(progress.Values[^1].IsCompleted, Is.True);
        });
    }

    [Test]
    public async Task RunOnceAsync_ReportsRunTransferAndActivityProgressForUpload()
    {
        LocalFileSnapshot local = LocalFile("Docs/local.txt", "local-content");
        var eventLog = new List<string>();
        var runProgress = new RecordingProgress<SyncRunProgress>(
            item => eventLog.Add($"run:{item.Stage}:{item.FilesCompleted}:{item.CurrentPath}:{item.IsCompleted}"));
        var transferProgress = new RecordingProgress<SyncTransferProgress>(
            item => eventLog.Add($"transfer:{item.Direction}:{item.RelativePath}:{item.TransferredBytes}:{item.TotalBytes}:{item.IsCompleted}"));
        var activityProgress = new RecordingProgress<SyncActivity>(
            item => eventLog.Add($"activity:{item.Kind}:{item.RelativePath}"));
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(local), EmptyRemoteTree(), new FakeRemoteFileSynchronizer(), out _);

        await engine.RunOnceAsync(
            Pair(),
            new SyncRunOptions
            {
                ActivityProgress = activityProgress,
                TransferProgress = transferProgress,
                RunProgress = runProgress,
            });

        int fileStartedIndex = eventLog.FindIndex(item => item.StartsWith("run:ReconcilingFiles:0:Docs/local.txt:", StringComparison.Ordinal));
        int transferStartedIndex = eventLog.FindIndex(item => item == $"transfer:Upload:Docs/local.txt:0:{local.SizeBytes}:False");
        int transferCompletedIndex = eventLog.FindIndex(item => item == $"transfer:Upload:Docs/local.txt:{local.SizeBytes}:{local.SizeBytes}:True");
        int activityIndex = eventLog.FindIndex(item => item == "activity:Uploaded:Docs/local.txt");
        int runCompletedIndex = eventLog.FindIndex(item => item == "run:Completed:1::True");
        Assert.Multiple(() =>
        {
            Assert.That(runProgress.Values.Select(item => item.Stage), Does.Contain(SyncRunProgressStage.Completed));
            Assert.That(transferProgress.Values.Select(item => item.IsCompleted), Is.EqualTo(new[] { false, true }));
            Assert.That(activityProgress.Values.Select(item => item.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(fileStartedIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(transferStartedIndex, Is.GreaterThan(fileStartedIndex));
            Assert.That(transferCompletedIndex, Is.GreaterThan(transferStartedIndex));
            Assert.That(activityIndex, Is.GreaterThan(transferCompletedIndex));
            Assert.That(runCompletedIndex, Is.GreaterThan(activityIndex));
        });
    }

    [Test]
    public async Task RunOnceAsync_DownloadsRemoteOnlyFileAndStoresBaseline()
    {
        byte[] content = Encoding.UTF8.GetBytes("remote-content");
        NodeFileManifestDto remote = RemoteFile("remote.txt", Hash(content), sizeBytes: content.Length);
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
    public async Task RunOnceAsync_CreatesRemoteFolderForLocalOnlyEmptyDirectoryAndStoresBaseline()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Projects", "Archive"));
        var scanner = new FakeLocalFileScanner
        {
            Directories =
            {
                LocalDirectory("Projects"),
                LocalDirectory("Projects/Archive"),
            },
        };
        var remoteDirectories = new FakeRemoteDirectorySynchronizer();
        SyncEngine engine = CreateEngine(
            scanner,
            EmptyRemoteTree(),
            new FakeRemoteFileSynchronizer(),
            out SqliteSyncStateStore stateStore,
            remoteDirectories);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(remoteDirectories.Creates, Has.Count.EqualTo(2));
            Assert.That(remoteDirectories.Creates[0].ParentNodeId, Is.EqualTo(_remoteRootNodeId));
            Assert.That(remoteDirectories.Creates[0].Name, Is.EqualTo("Projects"));
            Assert.That(remoteDirectories.Creates[1].ParentNodeId, Is.EqualTo(remoteDirectories.Creates[0].ReturnedNode.Id));
            Assert.That(remoteDirectories.Creates[1].Name, Is.EqualTo("Archive"));
            Assert.That(state.Select(entry => entry.Kind), Is.EqualTo(new[] { SyncEntryKind.Directory, SyncEntryKind.Directory }));
            Assert.That(state.Select(entry => entry.RelativePath), Is.EqualTo(new[] { "Projects", "Projects/Archive" }));
            Assert.That(state.Select(entry => entry.RemoteNodeId), Is.EqualTo(remoteDirectories.Creates.Select(call => call.ReturnedNode.Id)));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded, SyncActivityKind.Uploaded }));
        });
    }

    [Test]
    public async Task RunOnceAsync_CreatesLocalFolderForRemoteOnlyEmptyDirectoryAndStoresBaseline()
    {
        RemoteDirectorySnapshot remoteDirectory = RemoteDirectory("Projects");
        RemoteTreeSnapshot remoteTree = EmptyRemoteTree();
        remoteTree.Directories.Add(remoteDirectory);
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(),
            remoteTree,
            new FakeRemoteFileSynchronizer(),
            out SqliteSyncStateStore stateStore);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_root, "Projects")), Is.True);
            Assert.That(state, Has.Count.EqualTo(1));
            Assert.That(state[0].Kind, Is.EqualTo(SyncEntryKind.Directory));
            Assert.That(state[0].RelativePath, Is.EqualTo("Projects"));
            Assert.That(state[0].RemoteNodeId, Is.EqualTo(remoteDirectory.Node.Id));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
        });
    }

    [Test]
    public async Task RunOnceAsync_DeletesRemoteEmptyDirectoryWhenBaselineKnowsLocalDelete()
    {
        RemoteDirectorySnapshot remoteDirectory = RemoteDirectory("Projects");
        RemoteTreeSnapshot remoteTree = EmptyRemoteTree();
        remoteTree.Directories.Add(remoteDirectory);
        var remoteDirectories = new FakeRemoteDirectorySynchronizer();
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(),
            remoteTree,
            new FakeRemoteFileSynchronizer(),
            out SqliteSyncStateStore stateStore,
            remoteDirectories);
        await InsertDirectoryBaselineAsync(stateStore, "Projects", remoteDirectory.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(remoteDirectories.Deletes, Is.EqualTo(new[] { (remoteDirectory.Node.Id, false) }));
            Assert.That(state, Is.Empty);
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedRemote }));
        });
    }

    [Test]
    public async Task RunOnceAsync_DeletesLocalEmptyDirectoryWhenBaselineKnowsRemoteDelete()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Projects"));
        RemoteDirectorySnapshot remoteDirectory = RemoteDirectory("Projects");
        var scanner = new FakeLocalFileScanner
        {
            Directories =
            {
                LocalDirectory("Projects"),
            },
        };
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), new FakeRemoteFileSynchronizer(), out SqliteSyncStateStore stateStore);
        await InsertDirectoryBaselineAsync(stateStore, "Projects", remoteDirectory.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_root, "Projects")), Is.False);
            Assert.That(state, Is.Empty);
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedLocal }));
        });
    }

    [Test]
    public async Task RunOnceAsync_SkipsLocalDirectoryDeleteWhenFolderIsNotEmpty()
    {
        WriteFile("Projects/keep.txt", "keep");
        RemoteDirectorySnapshot remoteDirectory = RemoteDirectory("Projects");
        LocalFileSnapshot localFile = LocalFile("Projects/keep.txt", "keep");
        var scanner = new FakeLocalFileScanner(localFile)
        {
            Directories =
            {
                LocalDirectory("Projects"),
            },
        };
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), new FakeRemoteFileSynchronizer(), out SqliteSyncStateStore stateStore);
        await InsertDirectoryBaselineAsync(stateStore, "Projects", remoteDirectory.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? state = await stateStore.GetAsync("pair-a", "Projects");
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_root, "Projects")), Is.True);
            Assert.That(File.Exists(Path.Combine(_root, "Projects", "keep.txt")), Is.True);
            Assert.That(state, Is.Not.Null);
            Assert.That(result.RequiresUserAction, Is.False);
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Skipped, SyncActivityKind.Uploaded }));
            Assert.That(result.Activities[0].RequiresUserAction, Is.False);
            Assert.That(result.Activities[0].Details, Does.Contain("not empty"));
        });
    }

    [Test]
    public async Task RunOnceAsync_BlocksRemoteDirectoryDeletesOverRunLimit()
    {
        RemoteDirectorySnapshot first = RemoteDirectory("One");
        RemoteDirectorySnapshot second = RemoteDirectory("Two");
        RemoteTreeSnapshot remoteTree = EmptyRemoteTree();
        remoteTree.Directories.Add(first);
        remoteTree.Directories.Add(second);
        var remoteDirectories = new FakeRemoteDirectorySynchronizer();
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(),
            remoteTree,
            new FakeRemoteFileSynchronizer(),
            out SqliteSyncStateStore stateStore,
            remoteDirectories);
        await InsertDirectoryBaselineAsync(stateStore, "One", first.Node);
        await InsertDirectoryBaselineAsync(stateStore, "Two", second.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair(), new SyncRunOptions { MaximumRemoteDeletesPerRun = 1 });

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(remoteDirectories.Deletes, Is.Empty);
            Assert.That(state.Select(entry => entry.RelativePath), Is.EqualTo(new[] { "One", "Two" }));
            Assert.That(result.RequiresUserAction, Is.True);
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Skipped, SyncActivityKind.Skipped }));
            Assert.That(result.Activities.Select(activity => activity.RequiresUserAction), Is.All.True);
            Assert.That(result.Activities[0].Details, Does.Contain("2 pending deletes exceed limit 1"));
            Assert.That(result.Activities[1].Details, Does.Contain("2 pending deletes exceed limit 1"));
        });
    }

    [Test]
    public async Task RunOnceAsync_DoesNotCascadeRemoteDirectoryDeletesInsideOneRun()
    {
        RemoteDirectorySnapshot parent = RemoteDirectory("Projects");
        RemoteDirectorySnapshot child = RemoteDirectory("Projects/Archive", parent.Node.Id);
        RemoteTreeSnapshot remoteTree = EmptyRemoteTree();
        remoteTree.Directories.Add(parent);
        remoteTree.Directories.Add(child);
        var remoteDirectories = new FakeRemoteDirectorySynchronizer();
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(),
            remoteTree,
            new FakeRemoteFileSynchronizer(),
            out SqliteSyncStateStore stateStore,
            remoteDirectories);
        await InsertDirectoryBaselineAsync(stateStore, "Projects", parent.Node);
        await InsertDirectoryBaselineAsync(stateStore, "Projects/Archive", child.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair(), new SyncRunOptions { MaximumRemoteDeletesPerRun = 1 });

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(remoteDirectories.Deletes, Is.EqualTo(new[] { (child.Node.Id, false) }));
            Assert.That(state.Select(entry => entry.RelativePath), Is.EqualTo(new[] { "Projects" }));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedRemote, SyncActivityKind.Skipped }));
            Assert.That(result.Activities[1].Details, Does.Contain("not empty"));
        });
    }

    [Test]
    public async Task RunOnceAsync_DoesNotCascadeLocalDirectoryDeletesInsideOneRun()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Projects", "Archive"));
        RemoteDirectorySnapshot parent = RemoteDirectory("Projects");
        RemoteDirectorySnapshot child = RemoteDirectory("Projects/Archive", parent.Node.Id);
        var scanner = new FakeLocalFileScanner
        {
            Directories =
            {
                LocalDirectory("Projects"),
                LocalDirectory("Projects/Archive"),
            },
        };
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), new FakeRemoteFileSynchronizer(), out SqliteSyncStateStore stateStore);
        await InsertDirectoryBaselineAsync(stateStore, "Projects", parent.Node);
        await InsertDirectoryBaselineAsync(stateStore, "Projects/Archive", child.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair(), new SyncRunOptions { MaximumLocalDeletesPerRun = 1 });

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_root, "Projects")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(_root, "Projects", "Archive")), Is.False);
            Assert.That(state.Select(entry => entry.RelativePath), Is.EqualTo(new[] { "Projects" }));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedLocal, SyncActivityKind.Skipped }));
            Assert.That(result.Activities[1].Details, Does.Contain("not empty"));
        });
    }

    [Test]
    public async Task RunOnceAsync_PreservesLocalFolderWhenRemoteFileInsideIsDeleted()
    {
        const string directoryPath = "Projects";
        const string filePath = "Projects/deleted-remotely.txt";
        WriteFile(filePath, "baseline-content");
        LocalFileSnapshot local = LocalFile(filePath, "baseline-content");
        RemoteDirectorySnapshot remoteDirectory = RemoteDirectory(directoryPath);
        RemoteTreeSnapshot remoteTree = EmptyRemoteTree();
        remoteTree.Directories.Add(remoteDirectory);
        var scanner = new FakeLocalFileScanner(local)
        {
            Directories =
            {
                LocalDirectory(directoryPath),
            },
        };
        SyncEngine engine = CreateEngine(scanner, remoteTree, new FakeRemoteFileSynchronizer(), out SqliteSyncStateStore stateStore);
        await InsertDirectoryBaselineAsync(stateStore, directoryPath, remoteDirectory.Node);
        await InsertBaselineAsync(
            stateStore,
            filePath,
            local.ContentHash,
            RemoteFile(filePath, local.ContentHash));

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_root, directoryPath)), Is.True);
            Assert.That(File.Exists(Path.Combine(_root, "Projects", "deleted-remotely.txt")), Is.False);
            Assert.That(state.Select(entry => entry.RelativePath), Is.EqualTo(new[] { directoryPath }));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedLocal }));
        });
    }

    [Test]
    public async Task RunOnceAsync_PropagatesLocalEmptyDirectoryRenameAsCreateAndDelete()
    {
        const string oldPath = "Projects";
        const string newPath = "ProjectsRenamed";
        RemoteDirectorySnapshot oldRemoteDirectory = RemoteDirectory(oldPath);
        RemoteTreeSnapshot remoteTree = EmptyRemoteTree();
        remoteTree.Directories.Add(oldRemoteDirectory);
        var scanner = new FakeLocalFileScanner
        {
            Directories =
            {
                LocalDirectory(newPath),
            },
        };
        var remoteDirectories = new FakeRemoteDirectorySynchronizer();
        SyncEngine engine = CreateEngine(
            scanner,
            remoteTree,
            new FakeRemoteFileSynchronizer(),
            out SqliteSyncStateStore stateStore,
            remoteDirectories);
        await InsertDirectoryBaselineAsync(stateStore, oldPath, oldRemoteDirectory.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(remoteDirectories.Creates.Select(call => call.Name), Is.EqualTo(new[] { newPath }));
            Assert.That(remoteDirectories.Deletes, Is.EqualTo(new[] { (oldRemoteDirectory.Node.Id, false) }));
            Assert.That(state.Select(entry => entry.RelativePath), Is.EqualTo(new[] { newPath }));
            Assert.That(state[0].RemoteNodeId, Is.EqualTo(remoteDirectories.Creates[0].ReturnedNode.Id));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded, SyncActivityKind.DeletedRemote }));
        });
    }

    [Test]
    public async Task RunOnceAsync_PropagatesRemoteEmptyDirectoryRenameAsCreateAndDelete()
    {
        const string oldPath = "Projects";
        const string newPath = "ProjectsRenamed";
        Directory.CreateDirectory(Path.Combine(_root, oldPath));
        RemoteDirectorySnapshot oldRemoteDirectory = RemoteDirectory(oldPath);
        RemoteDirectorySnapshot newRemoteDirectory = RemoteDirectory(newPath);
        RemoteTreeSnapshot remoteTree = EmptyRemoteTree();
        remoteTree.Directories.Add(newRemoteDirectory);
        var scanner = new FakeLocalFileScanner
        {
            Directories =
            {
                LocalDirectory(oldPath),
            },
        };
        SyncEngine engine = CreateEngine(scanner, remoteTree, new FakeRemoteFileSynchronizer(), out SqliteSyncStateStore stateStore);
        await InsertDirectoryBaselineAsync(stateStore, oldPath, oldRemoteDirectory.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_root, oldPath)), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_root, newPath)), Is.True);
            Assert.That(state.Select(entry => entry.RelativePath), Is.EqualTo(new[] { newPath }));
            Assert.That(state[0].RemoteNodeId, Is.EqualTo(newRemoteDirectory.Node.Id));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded, SyncActivityKind.DeletedLocal }));
        });
    }

    [Test]
    public async Task RunOnceAsync_PropagatesLocalEmptyDirectoryMoveAsCreateAndDelete()
    {
        const string parentPath = "Archive";
        const string oldPath = "Projects";
        const string newPath = "Archive/Projects";
        RemoteDirectorySnapshot remoteParent = RemoteDirectory(parentPath);
        RemoteDirectorySnapshot oldRemoteDirectory = RemoteDirectory(oldPath);
        RemoteTreeSnapshot remoteTree = EmptyRemoteTree();
        remoteTree.Directories.Add(remoteParent);
        remoteTree.Directories.Add(oldRemoteDirectory);
        var scanner = new FakeLocalFileScanner
        {
            Directories =
            {
                LocalDirectory(parentPath),
                LocalDirectory(newPath),
            },
        };
        var remoteDirectories = new FakeRemoteDirectorySynchronizer();
        SyncEngine engine = CreateEngine(
            scanner,
            remoteTree,
            new FakeRemoteFileSynchronizer(),
            out SqliteSyncStateStore stateStore,
            remoteDirectories);
        await InsertDirectoryBaselineAsync(stateStore, parentPath, remoteParent.Node);
        await InsertDirectoryBaselineAsync(stateStore, oldPath, oldRemoteDirectory.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(remoteDirectories.Creates, Has.Count.EqualTo(1));
            Assert.That(remoteDirectories.Creates[0].ParentNodeId, Is.EqualTo(remoteParent.Node.Id));
            Assert.That(remoteDirectories.Creates[0].Name, Is.EqualTo("Projects"));
            Assert.That(remoteDirectories.Deletes, Is.EqualTo(new[] { (oldRemoteDirectory.Node.Id, false) }));
            Assert.That(state.Select(entry => entry.RelativePath), Is.EqualTo(new[] { parentPath, newPath }));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded, SyncActivityKind.DeletedRemote }));
        });
    }

    [Test]
    public async Task RunOnceAsync_PropagatesRemoteEmptyDirectoryMoveAsCreateAndDelete()
    {
        const string parentPath = "Archive";
        const string oldPath = "Projects";
        const string newPath = "Archive/Projects";
        Directory.CreateDirectory(Path.Combine(_root, parentPath));
        Directory.CreateDirectory(Path.Combine(_root, oldPath));
        RemoteDirectorySnapshot remoteParent = RemoteDirectory(parentPath);
        RemoteDirectorySnapshot oldRemoteDirectory = RemoteDirectory(oldPath);
        RemoteDirectorySnapshot movedRemoteDirectory = RemoteDirectory(newPath, remoteParent.Node.Id);
        RemoteTreeSnapshot remoteTree = EmptyRemoteTree();
        remoteTree.Directories.Add(remoteParent);
        remoteTree.Directories.Add(movedRemoteDirectory);
        var scanner = new FakeLocalFileScanner
        {
            Directories =
            {
                LocalDirectory(parentPath),
                LocalDirectory(oldPath),
            },
        };
        SyncEngine engine = CreateEngine(scanner, remoteTree, new FakeRemoteFileSynchronizer(), out SqliteSyncStateStore stateStore);
        await InsertDirectoryBaselineAsync(stateStore, parentPath, remoteParent.Node);
        await InsertDirectoryBaselineAsync(stateStore, oldPath, oldRemoteDirectory.Node);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> state = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_root, oldPath)), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_root, newPath.Replace('/', Path.DirectorySeparatorChar))), Is.True);
            Assert.That(state.Select(entry => entry.RelativePath), Is.EqualTo(new[] { parentPath, newPath }));
            Assert.That(state.Single(entry => entry.RelativePath == newPath).RemoteNodeId, Is.EqualTo(movedRemoteDirectory.Node.Id));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded, SyncActivityKind.DeletedLocal }));
        });
    }

    [Test]
    public async Task RunOnceAsync_UploadsUnicodeNamedLocalFileAndStoresBaseline()
    {
        const string relativePath = "Документы/設計-notes.txt";
        LocalFileSnapshot local = LocalFile(relativePath, "unicode-local-content");
        var scanner = new FakeLocalFileScanner(local);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), remoteFiles, out SqliteSyncStateStore stateStore);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(remoteFiles.Uploads, Has.Count.EqualTo(1));
            Assert.That(remoteFiles.Uploads[0].RelativePath, Is.EqualTo(relativePath));
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.RelativePath, Is.EqualTo(relativePath));
            Assert.That(entry.LocalContentHash, Is.EqualTo(local.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(local.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_DownloadsUnicodeNamedRemoteFileAndStoresBaseline()
    {
        const string relativePath = "Документы/設計-remote.txt";
        byte[] content = Encoding.UTF8.GetBytes("unicode-remote-content");
        NodeFileManifestDto remote = RemoteFile(relativePath, Hash(content), sizeBytes: content.Length);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.Downloads[remote.Id] = content;
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(_root, "Документы", "設計-remote.txt")), Is.EqualTo("unicode-remote-content"));
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Downloaded }));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.RelativePath, Is.EqualTo(relativePath));
            Assert.That(entry.LocalContentHash, Is.EqualTo(remote.ContentHash));
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
    public async Task RunOnceAsync_DoesNotUpdateBaselineWhenRemoteUploadFails()
    {
        string relativePath = "upload-fails.txt";
        LocalFileSnapshot local = LocalFile(relativePath, "local-new");
        NodeFileManifestDto remote = RemoteFile(relativePath, HashText("old"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.UploadFailureIds.Add(remote.Id);
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(local), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, relativePath, HashText("old"), remote);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.RunOnceAsync(Pair()));

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(HashText("old")));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(remote.ContentHash));
            Assert.That(remoteFiles.Uploads, Is.Empty);
        });
    }

    [Test]
    public async Task RunOnceAsync_RecoversAfterRemoteUploadBeforeBaselineUpdate()
    {
        string relativePath = "uploaded-before-baseline.txt";
        LocalFileSnapshot local = LocalFile(relativePath, "local-new");
        var scanner = new FakeLocalFileScanner(local);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        var durableStore = new SqliteSyncStateStore(_databasePath);
        var failingStore = new FailingUpsertStateStore(durableStore);
        SyncEngine firstRun = new(scanner, new FakeRemoteTreeCrawler(EmptyRemoteTree()), remoteFiles, failingStore);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await firstRun.RunOnceAsync(Pair()));

        NodeFileManifestDto uploaded = remoteFiles.Uploads.Single().ReturnedFile;
        SyncEngine secondRun = new(scanner, new FakeRemoteTreeCrawler(RemoteTree(uploaded)), remoteFiles, new SqliteSyncStateStore(_databasePath));
        SyncRunResult result = await secondRun.RunOnceAsync(Pair());

        SyncStateEntry? entry = await durableStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(remoteFiles.Uploads, Has.Count.EqualTo(1));
            Assert.That(result.Activities, Is.Empty);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(local.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(uploaded.ContentHash));
            Assert.That(entry.RemoteFileId, Is.EqualTo(uploaded.Id));
        });
    }

    [Test]
    public async Task RunOnceAsync_DownloadsRemoteChangeWhenLocalBaselineIsUnchanged()
    {
        string relativePath = "changed-down.txt";
        WriteFile(relativePath, "old");
        LocalFileSnapshot local = LocalFile(relativePath, "old");
        byte[] remoteContent = Encoding.UTF8.GetBytes("remote-new");
        NodeFileManifestDto remote = RemoteFile(relativePath, Hash(remoteContent), sizeBytes: remoteContent.Length);
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
    public async Task RunOnceAsync_DoesNotUpdateBaselineWhenRemoteDownloadFails()
    {
        string relativePath = "download-fails.txt";
        WriteFile(relativePath, "old");
        LocalFileSnapshot local = LocalFile(relativePath, "old");
        NodeFileManifestDto remote = RemoteFile(
            relativePath,
            HashText("remote-new"),
            sizeBytes: Encoding.UTF8.GetByteCount("remote-new"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.DownloadFailureIds.Add(remote.Id);
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(local), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, relativePath, local.ContentHash, RemoteFile(relativePath, local.ContentHash, remote.Id));

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.RunOnceAsync(Pair()));

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        string temporaryDirectory = Path.Combine(_root, ".cotton-sync", "tmp");
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(File.ReadAllText(Path.Combine(_root, relativePath)), Is.EqualTo("old"));
            Assert.That(
                Directory.Exists(temporaryDirectory)
                    ? Directory.GetFiles(temporaryDirectory, "*", SearchOption.AllDirectories)
                    : [],
                Is.Empty);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(local.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(local.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_RejectsDownloadedContentThatDoesNotMatchManifest()
    {
        string relativePath = "download-corrupt.txt";
        byte[] expectedContent = Encoding.UTF8.GetBytes("complete remote file");
        byte[] partialContent = Encoding.UTF8.GetBytes("partial");
        NodeFileManifestDto remote = RemoteFile(
            relativePath,
            Hash(expectedContent),
            sizeBytes: expectedContent.Length);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.Downloads[remote.Id] = partialContent;
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);

        InvalidDataException? exception = Assert.ThrowsAsync<InvalidDataException>(
            async () => await engine.RunOnceAsync(Pair()));

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        string temporaryDirectory = Path.Combine(_root, ".cotton-sync", "tmp");
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(_root, relativePath)), Is.False);
            Assert.That(entry, Is.Null);
            Assert.That(
                Directory.Exists(temporaryDirectory)
                    ? Directory.GetFiles(temporaryDirectory, "*", SearchOption.AllDirectories)
                    : [],
                Is.Empty);
        });
    }

    [Test]
    public async Task RunOnceAsync_RecoversAfterRemoteDownloadBeforeBaselineUpdate()
    {
        string relativePath = "downloaded-before-baseline.txt";
        byte[] remoteContent = Encoding.UTF8.GetBytes("remote-new");
        NodeFileManifestDto remote = RemoteFile(relativePath, Hash(remoteContent), sizeBytes: remoteContent.Length);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.Downloads[remote.Id] = remoteContent;
        var durableStore = new SqliteSyncStateStore(_databasePath);
        var failingStore = new FailingUpsertStateStore(durableStore);
        SyncEngine firstRun = new(
            new FakeLocalFileScanner(),
            new FakeRemoteTreeCrawler(RemoteTree(remote)),
            remoteFiles,
            failingStore);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await firstRun.RunOnceAsync(Pair()));

        IReadOnlyList<SyncStateEntry> entriesAfterCrash = await durableStore.LoadPairAsync("pair-a");
        LocalFileSnapshot downloadedLocal = LocalFile(relativePath, "remote-new");
        SyncEngine secondRun = new(
            new FakeLocalFileScanner(downloadedLocal),
            new FakeRemoteTreeCrawler(RemoteTree(remote)),
            remoteFiles,
            durableStore);

        SyncRunResult result = await secondRun.RunOnceAsync(Pair());

        SyncStateEntry? entry = await durableStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(File.ReadAllText(Path.Combine(_root, relativePath)), Is.EqualTo("remote-new"));
            Assert.That(entriesAfterCrash, Is.Empty);
            Assert.That(result.Activities, Is.Empty);
            Assert.That(remoteFiles.Uploads, Is.Empty);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(remote.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(remote.ContentHash));
            Assert.That(entry.RemoteFileId, Is.EqualTo(remote.Id));
        });
    }

    [Test]
    public async Task RunOnceAsync_DeletesRemoteOnlyWhenBaselineKnowsLocalDelete()
    {
        NodeFileManifestDto remote = RemoteFile("delete-remote.txt", HashText("old"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, "delete-remote.txt", remote.ContentHash, remote);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", "delete-remote.txt");
        Assert.Multiple(() =>
        {
            Assert.That(remoteFiles.Deletes, Is.EqualTo(new[] { (remote.Id, false, remote.ETag) }));
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedRemote }));
            Assert.That(entry, Is.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_CanBypassRemoteTrashWhenExplicitlyConfigured()
    {
        NodeFileManifestDto remote = RemoteFile("delete-remote-permanent.txt", HashText("old"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, "delete-remote-permanent.txt", remote.ContentHash, remote);

        SyncRunResult result = await engine.RunOnceAsync(Pair(), new SyncRunOptions { DeleteRemotePermanently = true });

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", "delete-remote-permanent.txt");
        Assert.Multiple(() =>
        {
            Assert.That(remoteFiles.Deletes, Is.EqualTo(new[] { (remote.Id, true, remote.ETag) }));
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.DeletedRemote }));
            Assert.That(entry, Is.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_DoesNotDeleteBaselineWhenRemoteDeleteFails()
    {
        string relativePath = "delete-remote-fails.txt";
        NodeFileManifestDto remote = RemoteFile(relativePath, HashText("old"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.DeleteFailureIds.Add(remote.Id);
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), RemoteTree(remote), remoteFiles, out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, relativePath, remote.ContentHash, remote);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.RunOnceAsync(Pair()));

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(remoteFiles.Deletes, Is.EqualTo(new[] { (remote.Id, false, remote.ETag) }));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.RemoteFileId, Is.EqualTo(remote.Id));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(remote.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_RecoversAfterRemoteDeleteBeforeBaselineDelete()
    {
        string relativePath = "remote-deleted-before-baseline.txt";
        NodeFileManifestDto remote = RemoteFile(relativePath, HashText("old"));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        var durableStore = new SqliteSyncStateStore(_databasePath);
        await InsertBaselineAsync(durableStore, relativePath, remote.ContentHash, remote);
        var failingStore = new FailingDeleteStateStore(durableStore);
        SyncEngine firstRun = new(
            new FakeLocalFileScanner(),
            new FakeRemoteTreeCrawler(RemoteTree(remote)),
            remoteFiles,
            failingStore);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await firstRun.RunOnceAsync(Pair()));

        SyncStateEntry? staleEntry = await durableStore.GetAsync("pair-a", relativePath);
        SyncEngine secondRun = new(
            new FakeLocalFileScanner(),
            new FakeRemoteTreeCrawler(EmptyRemoteTree()),
            remoteFiles,
            durableStore);
        SyncRunResult result = await secondRun.RunOnceAsync(Pair());

        SyncStateEntry? entry = await durableStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(staleEntry, Is.Not.Null);
            Assert.That(remoteFiles.Deletes, Is.EqualTo(new[] { (remote.Id, false, remote.ETag) }));
            Assert.That(result.Activities, Is.Empty);
            Assert.That(entry, Is.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_RecoversAfterLocalDeleteBeforeBaselineDelete()
    {
        string relativePath = "local-deleted-before-baseline.txt";
        WriteFile(relativePath, "old");
        LocalFileSnapshot local = LocalFile(relativePath, "old");
        NodeFileManifestDto remote = RemoteFile(relativePath, local.ContentHash);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        var durableStore = new SqliteSyncStateStore(_databasePath);
        await InsertBaselineAsync(durableStore, relativePath, local.ContentHash, remote);
        var failingStore = new FailingDeleteStateStore(durableStore);
        SyncEngine firstRun = new(
            new FakeLocalFileScanner(local),
            new FakeRemoteTreeCrawler(EmptyRemoteTree()),
            remoteFiles,
            failingStore);

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await firstRun.RunOnceAsync(Pair()));

        SyncStateEntry? staleEntry = await durableStore.GetAsync("pair-a", relativePath);
        SyncEngine secondRun = new(
            new FakeLocalFileScanner(),
            new FakeRemoteTreeCrawler(EmptyRemoteTree()),
            remoteFiles,
            durableStore);
        SyncRunResult result = await secondRun.RunOnceAsync(Pair());

        SyncStateEntry? entry = await durableStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(_root, relativePath)), Is.False);
            Assert.That(staleEntry, Is.Not.Null);
            Assert.That(remoteFiles.Uploads, Is.Empty);
            Assert.That(remoteFiles.Deletes, Is.Empty);
            Assert.That(result.Activities, Is.Empty);
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
            Assert.That(remoteFiles.Deletes, Is.Empty);
            Assert.That(result.RequiresUserAction, Is.True);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[]
            {
                SyncActivityKind.Skipped,
                SyncActivityKind.Skipped,
            }));
            Assert.That(result.Activities.Select(activity => activity.RequiresUserAction), Is.All.True);
            Assert.That(result.Activities[0].Details, Does.Contain("2 pending deletes exceed limit 1"));
            Assert.That(result.Activities[1].Details, Does.Contain("2 pending deletes exceed limit 1"));
            Assert.That(firstEntry, Is.Not.Null);
            Assert.That(secondEntry, Is.Not.Null);
        });
    }

    [Test]
    public async Task RunOnceAsync_DownloadsRemoteFileInsteadOfDeletingWhenBaselineIsMissing()
    {
        byte[] content = Encoding.UTF8.GetBytes("no-baseline-remote");
        NodeFileManifestDto remote = RemoteFile("safe-download.txt", Hash(content), sizeBytes: content.Length);
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
            Assert.That(File.Exists(Path.Combine(_root, "a.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(_root, "b.txt")), Is.True);
            Assert.That(result.RequiresUserAction, Is.True);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[]
            {
                SyncActivityKind.Skipped,
                SyncActivityKind.Skipped,
            }));
            Assert.That(result.Activities.Select(activity => activity.RequiresUserAction), Is.All.True);
            Assert.That(result.Activities[0].Details, Does.Contain("2 pending deletes exceed limit 1"));
            Assert.That(result.Activities[1].Details, Does.Contain("2 pending deletes exceed limit 1"));
            Assert.That(firstEntry, Is.Not.Null);
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
        NodeFileManifestDto remote = RemoteFile(relativePath, Hash(remoteContent), sizeBytes: remoteContent.Length);
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

    [TestCase(MatrixFileState.Missing, MatrixFileState.Missing, 0)]
    [TestCase(MatrixFileState.Missing, MatrixFileState.Baseline, (int)SyncActivityKind.DeletedRemote)]
    [TestCase(MatrixFileState.Missing, MatrixFileState.Changed, (int)SyncActivityKind.Conflict)]
    [TestCase(MatrixFileState.Baseline, MatrixFileState.Missing, (int)SyncActivityKind.DeletedLocal)]
    [TestCase(MatrixFileState.Baseline, MatrixFileState.Baseline, 0)]
    [TestCase(MatrixFileState.Baseline, MatrixFileState.Changed, (int)SyncActivityKind.Downloaded)]
    [TestCase(MatrixFileState.Changed, MatrixFileState.Missing, (int)SyncActivityKind.Conflict)]
    [TestCase(MatrixFileState.Changed, MatrixFileState.Baseline, (int)SyncActivityKind.Uploaded)]
    [TestCase(MatrixFileState.Changed, MatrixFileState.Changed, (int)SyncActivityKind.Conflict)]
    public async Task RunOnceAsync_ReconcilesBaselineMatrix(
        MatrixFileState localState,
        MatrixFileState remoteState,
        int expectedActivityKind)
    {
        string relativePath = $"matrix/{localState}-{remoteState}.txt";
        string baselineContent = "base";
        string localContent = localState == MatrixFileState.Changed ? "local-changed" : baselineContent;
        string remoteContent = remoteState == MatrixFileState.Changed ? "remote-changed" : baselineContent;
        Guid remoteId = Guid.NewGuid();
        NodeFileManifestDto baselineRemote = RemoteFile(relativePath, HashText(baselineContent), remoteId);
        LocalFileSnapshot? local = CreateMatrixLocal(relativePath, localState, localContent);
        NodeFileManifestDto? remote = remoteState == MatrixFileState.Missing
            ? null
            : RemoteFile(relativePath, HashText(remoteContent), remoteId, Encoding.UTF8.GetByteCount(remoteContent));
        var remoteFiles = new FakeRemoteFileSynchronizer();
        if (remote is not null && remoteState == MatrixFileState.Changed)
        {
            remoteFiles.Downloads[remote.Id] = Encoding.UTF8.GetBytes(remoteContent);
        }

        LocalFileSnapshot[] localFiles = local is null ? [] : [local];
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(localFiles),
            remote is null ? EmptyRemoteTree() : RemoteTree(remote),
            remoteFiles,
            out SqliteSyncStateStore stateStore);
        await InsertBaselineAsync(stateStore, relativePath, HashText(baselineContent), baselineRemote);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncActivityKind> expectedKinds = expectedActivityKind == 0
            ? []
            : [(SyncActivityKind)expectedActivityKind];
        Assert.Multiple(() =>
        {
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(expectedKinds));
            AssertMatrixSideEffects(relativePath, localState, remoteState, remoteFiles);
        });
    }

    [Test]
    public async Task RunOnceAsync_PreservesBothVersionsWhenStaleUploadLosesRemoteRace()
    {
        string relativePath = "stale-upload.txt";
        WriteFile(relativePath, "local-new");
        LocalFileSnapshot local = LocalFile(relativePath, "local-new");
        Guid remoteId = Guid.NewGuid();
        NodeFileManifestDto baselineRemote = RemoteFile(relativePath, HashText("old"), remoteId);
        NodeFileManifestDto initialRemote = RemoteFile(relativePath, HashText("old"), remoteId);
        byte[] latestRemoteContent = Encoding.UTF8.GetBytes("remote-new");
        NodeFileManifestDto latestRemote = RemoteFile(relativePath, Hash(latestRemoteContent), remoteId, latestRemoteContent.Length);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.PreconditionFailedUploadIds.Add(remoteId);
        remoteFiles.Downloads[remoteId] = latestRemoteContent;
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(local),
            remoteFiles,
            out SqliteSyncStateStore stateStore,
            RemoteTree(initialRemote),
            RemoteTree(latestRemote));
        await InsertBaselineAsync(stateStore, relativePath, baselineRemote.ContentHash, baselineRemote);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        string[] conflictFiles = Directory.GetFiles(_root, "*Cotton conflict*", SearchOption.AllDirectories);
        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(_root, relativePath)), Is.EqualTo("local-new"));
            Assert.That(conflictFiles, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(conflictFiles[0]), Is.EqualTo("remote-new"));
            Assert.That(remoteFiles.Uploads, Is.Empty);
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Conflict }));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(local.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(latestRemote.ContentHash));
        });
    }

    [Test]
    public async Task RunOnceAsync_RestoresRemoteVersionWhenStaleDeleteLosesRemoteRace()
    {
        string relativePath = "stale-delete.txt";
        Guid remoteId = Guid.NewGuid();
        NodeFileManifestDto baselineRemote = RemoteFile(relativePath, HashText("old"), remoteId);
        NodeFileManifestDto initialRemote = RemoteFile(relativePath, HashText("old"), remoteId);
        byte[] latestRemoteContent = Encoding.UTF8.GetBytes("remote-new");
        NodeFileManifestDto latestRemote = RemoteFile(relativePath, Hash(latestRemoteContent), remoteId, latestRemoteContent.Length);
        var remoteFiles = new FakeRemoteFileSynchronizer();
        remoteFiles.PreconditionFailedDeleteIds.Add(remoteId);
        remoteFiles.Downloads[remoteId] = latestRemoteContent;
        SyncEngine engine = CreateEngine(
            new FakeLocalFileScanner(),
            remoteFiles,
            out SqliteSyncStateStore stateStore,
            RemoteTree(initialRemote),
            RemoteTree(latestRemote));
        await InsertBaselineAsync(stateStore, relativePath, baselineRemote.ContentHash, baselineRemote);

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        SyncStateEntry? entry = await stateStore.GetAsync("pair-a", relativePath);
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(_root, relativePath)), Is.EqualTo("remote-new"));
            Assert.That(remoteFiles.Deletes, Is.EqualTo(new[] { (remoteId, false, initialRemote.ETag) }));
            Assert.That(result.Activities.Select(x => x.Kind), Is.EqualTo(new[] { SyncActivityKind.Conflict }));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.LocalContentHash, Is.EqualTo(latestRemote.ContentHash));
            Assert.That(entry.RemoteContentHash, Is.EqualTo(latestRemote.ContentHash));
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
        NodeFileManifestDto remote = RemoteFile(relativePath, Hash(remoteContent), sizeBytes: remoteContent.Length);
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

    [Test]
    public void RunOnceAsync_RejectsLocalCaseInsensitivePathCollision()
    {
        var scanner = new FakeLocalFileScanner(
            LocalFile("Case.txt", "first"),
            LocalFile("case.txt", "second"));
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), new FakeRemoteFileSynchronizer(), out _);

        SyncPathCollisionException? exception = Assert.ThrowsAsync<SyncPathCollisionException>(() => engine.RunOnceAsync(Pair()));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.FirstPath, Is.EqualTo("Case.txt"));
            Assert.That(exception.SecondPath, Is.EqualTo("case.txt"));
            Assert.That(exception.Message, Does.Contain("Case-insensitive path collision"));
            Assert.That(exception.Message, Does.Contain("Case.txt"));
            Assert.That(exception.Message, Does.Contain("case.txt"));
        });
    }

    [Test]
    public void RunOnceAsync_RejectsLocalFileDirectoryCaseInsensitivePathCollision()
    {
        var scanner = new FakeLocalFileScanner(LocalFile("Project", "file"));
        scanner.Directories.Add(LocalDirectory("project"));
        SyncEngine engine = CreateEngine(scanner, EmptyRemoteTree(), new FakeRemoteFileSynchronizer(), out _);

        SyncPathCollisionException? exception = Assert.ThrowsAsync<SyncPathCollisionException>(() => engine.RunOnceAsync(Pair()));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.FirstPath, Is.EqualTo("project"));
            Assert.That(exception.SecondPath, Is.EqualTo("Project"));
            Assert.That(exception.Message, Does.Contain("Case-insensitive path collision"));
        });
    }

    [Test]
    public void RunOnceAsync_RejectsRemoteCaseInsensitivePathCollision()
    {
        RemoteTreeSnapshot remoteTree = RemoteTree(
            RemoteFile("Remote.txt", HashText("first")),
            RemoteFile("remote.txt", HashText("second")));
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), remoteTree, new FakeRemoteFileSynchronizer(), out _);

        SyncPathCollisionException? exception = Assert.ThrowsAsync<SyncPathCollisionException>(() => engine.RunOnceAsync(Pair()));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.FirstPath, Is.EqualTo("Remote.txt"));
            Assert.That(exception.SecondPath, Is.EqualTo("remote.txt"));
            Assert.That(exception.Message, Does.Contain("Case-insensitive path collision"));
            Assert.That(exception.Message, Does.Contain("Remote.txt"));
            Assert.That(exception.Message, Does.Contain("remote.txt"));
        });
    }

    [Test]
    public void RunOnceAsync_RejectsRemoteFileDirectoryCaseInsensitivePathCollision()
    {
        RemoteTreeSnapshot remoteTree = RemoteTree(RemoteFile("Remote", HashText("file")));
        remoteTree.Directories.Add(RemoteDirectory("remote"));
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), remoteTree, new FakeRemoteFileSynchronizer(), out _);

        SyncPathCollisionException? exception = Assert.ThrowsAsync<SyncPathCollisionException>(() => engine.RunOnceAsync(Pair()));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.FirstPath, Is.EqualTo("remote"));
            Assert.That(exception.SecondPath, Is.EqualTo("Remote"));
            Assert.That(exception.Message, Does.Contain("Case-insensitive path collision"));
        });
    }

    [Test]
    public async Task RunOnceAsync_IgnoresRemoteMetadataPathsAtEngineBoundary()
    {
        NodeFileManifestDto remote = RemoteFile(".cotton-sync/remote-file.txt", HashText("remote"));
        SyncEngine engine = CreateEngine(new FakeLocalFileScanner(), RemoteTree(remote), new FakeRemoteFileSynchronizer(), out SqliteSyncStateStore stateStore);
        await stateStore.InitializeAsync();
        await stateStore.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = ".cotton-sync/remote-file.txt",
            Kind = SyncEntryKind.File,
            RemoteFileId = remote.Id,
            RemoteNodeId = remote.NodeId,
            RemoteContentHash = remote.ContentHash,
            RemoteETag = remote.ETag,
        });

        SyncRunResult result = await engine.RunOnceAsync(Pair());

        IReadOnlyList<SyncStateEntry> entries = await stateStore.LoadPairAsync("pair-a");
        Assert.Multiple(() =>
        {
            Assert.That(result.Activities, Is.Empty);
            Assert.That(entries, Is.Empty);
            Assert.That(File.Exists(Path.Combine(_root, ".cotton-sync", "remote-file.txt")), Is.False);
        });
    }

    private SyncEngine CreateEngine(
        ILocalFileScanner scanner,
        RemoteTreeSnapshot remoteTree,
        FakeRemoteFileSynchronizer remoteFiles,
        out SqliteSyncStateStore stateStore,
        ILogger<SyncEngine>? logger = null)
    {
        return CreateEngineWithLogger(scanner, remoteFiles, out stateStore, logger, remoteTree);
    }

    private SyncEngine CreateEngine(
        ILocalFileScanner scanner,
        FakeRemoteFileSynchronizer remoteFiles,
        out SqliteSyncStateStore stateStore,
        params RemoteTreeSnapshot[] remoteTrees)
    {
        return CreateEngineWithLogger(scanner, remoteFiles, out stateStore, null, remoteTrees);
    }

    private SyncEngine CreateEngineWithLogger(
        ILocalFileScanner scanner,
        FakeRemoteFileSynchronizer remoteFiles,
        out SqliteSyncStateStore stateStore,
        ILogger<SyncEngine>? logger,
        params RemoteTreeSnapshot[] remoteTrees)
    {
        stateStore = new SqliteSyncStateStore(_databasePath);
        return new SyncEngine(scanner, new FakeRemoteTreeCrawler(remoteTrees), remoteFiles, stateStore, logger: logger);
    }

    private SyncEngine CreateEngine(
        ILocalFileScanner scanner,
        RemoteTreeSnapshot remoteTree,
        FakeRemoteFileSynchronizer remoteFiles,
        out SqliteSyncStateStore stateStore,
        FakeRemoteDirectorySynchronizer remoteDirectories,
        ILogger<SyncEngine>? logger = null)
    {
        stateStore = new SqliteSyncStateStore(_databasePath);
        return new SyncEngine(
            scanner,
            new FakeRemoteTreeCrawler(remoteTree),
            remoteFiles,
            stateStore,
            remoteDirectories: remoteDirectories,
            logger: logger);
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

    private async Task InsertDirectoryBaselineAsync(
        SqliteSyncStateStore stateStore,
        string relativePath,
        NodeDto remoteNode)
    {
        await stateStore.InitializeAsync();
        await stateStore.UpsertAsync(new SyncStateEntry
        {
            SyncPairId = "pair-a",
            RelativePath = relativePath,
            Kind = SyncEntryKind.Directory,
            RemoteNodeId = remoteNode.Id,
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

    private LocalDirectorySnapshot LocalDirectory(string relativePath)
    {
        return new LocalDirectorySnapshot
        {
            RelativePath = relativePath.Replace('\\', '/'),
            FullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)),
        };
    }

    private void WriteFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetLastWriteTimeUtc(fullPath, new DateTime(2026, 6, 2, 13, 0, 0, DateTimeKind.Utc));
    }

    private LocalFileSnapshot? CreateMatrixLocal(string relativePath, MatrixFileState state, string content)
    {
        if (state == MatrixFileState.Missing)
        {
            return null;
        }

        WriteFile(relativePath, content);
        return LocalFile(relativePath, content);
    }

    private void AssertMatrixSideEffects(
        string relativePath,
        MatrixFileState localState,
        MatrixFileState remoteState,
        FakeRemoteFileSynchronizer remoteFiles)
    {
        string fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (localState == MatrixFileState.Missing && remoteState == MatrixFileState.Baseline)
        {
            Assert.That(remoteFiles.Deletes, Has.Count.EqualTo(1));
        }
        else if (localState == MatrixFileState.Baseline && remoteState == MatrixFileState.Missing)
        {
            Assert.That(File.Exists(fullPath), Is.False);
        }
        else if (localState == MatrixFileState.Baseline && remoteState == MatrixFileState.Changed)
        {
            Assert.That(File.ReadAllText(fullPath), Is.EqualTo("remote-changed"));
        }
        else if (localState == MatrixFileState.Changed && remoteState is MatrixFileState.Missing or MatrixFileState.Baseline)
        {
            Assert.That(remoteFiles.Uploads, Has.Count.EqualTo(1));
        }
        else if (localState == MatrixFileState.Changed && remoteState == MatrixFileState.Changed)
        {
            string[] conflictFiles = Directory.GetFiles(_root, "*Cotton conflict*", SearchOption.AllDirectories);
            Assert.That(File.ReadAllText(fullPath), Is.EqualTo("local-changed"));
            Assert.That(conflictFiles, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(conflictFiles[0]), Is.EqualTo("remote-changed"));
        }
        else if (localState == MatrixFileState.Missing && remoteState == MatrixFileState.Changed)
        {
            Assert.That(File.ReadAllText(fullPath), Is.EqualTo("remote-changed"));
        }
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

    private RemoteDirectorySnapshot RemoteDirectory(string relativePath, Guid? parentNodeId = null)
    {
        return new RemoteDirectorySnapshot
        {
            RelativePath = relativePath.Replace('\\', '/'),
            Node = new NodeDto
            {
                Id = Guid.NewGuid(),
                ParentId = parentNodeId ?? _remoteRootNodeId,
                Name = relativePath.Split('/')[^1],
            },
        };
    }

    private NodeFileManifestDto RemoteFile(string relativePath, string contentHash, Guid? id = null, long sizeBytes = 1)
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
            SizeBytes = sizeBytes,
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

    private sealed class FakeLocalFileScanner : ILocalFileScanner, ILocalTreeScanner
    {
        public FakeLocalFileScanner(params LocalFileSnapshot[] files)
        {
            Files = files.ToList();
        }

        public List<LocalDirectorySnapshot> Directories { get; } = [];

        public List<LocalFileSnapshot> Files { get; }

        public int ScanCalls { get; private set; }

        public Task<IReadOnlyList<LocalFileSnapshot>> ScanAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            ScanCalls++;
            return Task.FromResult<IReadOnlyList<LocalFileSnapshot>>(Files);
        }

        public Task<LocalTreeSnapshot> ScanTreeAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            ScanCalls++;
            return Task.FromResult(new LocalTreeSnapshot
            {
                Directories = Directories,
                Files = Files,
            });
        }
    }

    private sealed class MetadataOnlyLocalFileScanner : ILocalFileScanner, ILocalTreeScanner, ILocalFileMetadataTreeScanner, ILocalFileContentHasher
    {
        public MetadataOnlyLocalFileScanner(params LocalFileSnapshot[] files)
        {
            Files = files.ToList();
        }

        public List<LocalFileSnapshot> Files { get; }

        public int ContentHashCalls { get; private set; }

        public Task<IReadOnlyList<LocalFileSnapshot>> ScanAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LocalFileSnapshot>>(Files);
        }

        public Task<LocalTreeSnapshot> ScanTreeAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LocalTreeSnapshot
            {
                Files = Files,
            });
        }

        public Task<LocalTreeSnapshot> ScanTreeMetadataAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LocalTreeSnapshot
            {
                Files = Files,
            });
        }

        public Task<string> ComputeContentHashAsync(LocalFileSnapshot localFile, CancellationToken cancellationToken = default)
        {
            ContentHashCalls++;
            return Task.FromResult("precomputed-content-hash");
        }
    }

    private sealed class FakeRemoteTreeCrawler : IRemoteTreeCrawler
    {
        private readonly Queue<RemoteTreeSnapshot> _snapshots;
        private RemoteTreeSnapshot _lastSnapshot;

        public FakeRemoteTreeCrawler(params RemoteTreeSnapshot[] snapshots)
        {
            if (snapshots.Length == 0)
            {
                throw new ArgumentException("At least one remote snapshot is required.", nameof(snapshots));
            }

            _snapshots = new Queue<RemoteTreeSnapshot>(snapshots);
            _lastSnapshot = snapshots[0];
        }

        public Task<RemoteTreeSnapshot> CrawlAsync(Guid rootNodeId, CancellationToken cancellationToken = default)
        {
            if (_snapshots.Count > 0)
            {
                _lastSnapshot = _snapshots.Dequeue();
            }

            return Task.FromResult(_lastSnapshot);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        private readonly Action<T>? _onReport;

        public RecordingProgress(Action<T>? onReport = null)
        {
            _onReport = onReport;
        }

        public List<T> Values { get; } = [];

        public void Report(T value)
        {
            Values.Add(value);
            _onReport?.Invoke(value);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class FakeRemoteFileSynchronizer : IRemoteFileSynchronizer
    {
        public List<UploadCall> Uploads { get; } = [];

        public List<string> UploadInputContentHashes { get; } = [];

        public List<(Guid NodeFileId, bool SkipTrash, string? ExpectedETag)> Deletes { get; } = [];

        public Dictionary<Guid, byte[]> Downloads { get; } = [];

        public HashSet<Guid> UploadFailureIds { get; } = [];

        public HashSet<Guid> DownloadFailureIds { get; } = [];

        public HashSet<Guid> DeleteFailureIds { get; } = [];

        public HashSet<Guid> PreconditionFailedUploadIds { get; } = [];

        public HashSet<Guid> PreconditionFailedDeleteIds { get; } = [];

        public string? EmptyLocalHashUploadContentHash { get; set; }

        public Task<NodeFileManifestDto> UploadFileAsync(
            Guid rootNodeId,
            string relativePath,
            LocalFileSnapshot localFile,
            NodeFileManifestDto? existingRemoteFile = null,
            CancellationToken cancellationToken = default)
        {
            if (existingRemoteFile is not null && PreconditionFailedUploadIds.Contains(existingRemoteFile.Id))
            {
                throw new HttpRequestException(
                    "Remote file changed before upload.",
                    inner: null,
                    HttpStatusCode.PreconditionFailed);
            }

            if (existingRemoteFile is not null && UploadFailureIds.Contains(existingRemoteFile.Id))
            {
                throw new InvalidOperationException("Remote upload failed.");
            }

            UploadInputContentHashes.Add(localFile.ContentHash);
            string uploadedContentHash = string.IsNullOrWhiteSpace(localFile.ContentHash)
                ? EmptyLocalHashUploadContentHash ?? localFile.ContentHash
                : localFile.ContentHash;
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
                ContentHash = uploadedContentHash,
                ETag = "sha256-" + uploadedContentHash,
                CreatedAt = new DateTime(2026, 6, 2, 14, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 6, 2, 14, 0, 0, DateTimeKind.Utc),
                Metadata = new Dictionary<string, string> { ["relativePath"] = relativePath.Replace('\\', '/') },
            };
            Uploads.Add(new UploadCall(rootNodeId, relativePath, localFile, existingRemoteFile, returned));
            return Task.FromResult(returned);
        }

        public Task DownloadFileAsync(Guid nodeFileId, Stream destination, CancellationToken cancellationToken = default)
        {
            if (DownloadFailureIds.Contains(nodeFileId))
            {
                throw new InvalidOperationException("Remote download failed.");
            }

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
            if (DeleteFailureIds.Contains(nodeFileId))
            {
                throw new InvalidOperationException("Remote delete failed.");
            }

            if (PreconditionFailedDeleteIds.Contains(nodeFileId))
            {
                throw new HttpRequestException(
                    "Remote file changed before delete.",
                    inner: null,
                    HttpStatusCode.PreconditionFailed);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeRemoteDirectorySynchronizer : IRemoteDirectorySynchronizer
    {
        public List<CreateDirectoryCall> Creates { get; } = [];

        public List<(Guid NodeId, bool SkipTrash)> Deletes { get; } = [];

        public Task<NodeDto> CreateDirectoryAsync(
            Guid parentNodeId,
            string name,
            CancellationToken cancellationToken = default)
        {
            NodeDto node = new()
            {
                Id = Guid.NewGuid(),
                ParentId = parentNodeId,
                Name = name,
            };
            Creates.Add(new CreateDirectoryCall(parentNodeId, name, node));
            return Task.FromResult(node);
        }

        public Task DeleteDirectoryAsync(Guid nodeId, bool skipTrash = false, CancellationToken cancellationToken = default)
        {
            Deletes.Add((nodeId, skipTrash));
            return Task.CompletedTask;
        }
    }

    private sealed record CreateDirectoryCall(Guid ParentNodeId, string Name, NodeDto ReturnedNode);

    private sealed record UploadCall(
        Guid RootNodeId,
        string RelativePath,
        LocalFileSnapshot LocalFile,
        NodeFileManifestDto? ExistingRemoteFile,
        NodeFileManifestDto ReturnedFile);

    private abstract class DelegatingStateStore : ISyncStateStore
    {
        private readonly ISyncStateStore _inner;

        protected DelegatingStateStore(ISyncStateStore inner)
        {
            _inner = inner;
        }

        public virtual Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return _inner.InitializeAsync(cancellationToken);
        }

        public virtual Task<IReadOnlyList<SyncStateEntry>> LoadPairAsync(string syncPairId, CancellationToken cancellationToken = default)
        {
            return _inner.LoadPairAsync(syncPairId, cancellationToken);
        }

        public virtual Task<SyncChangeCursor> GetChangeCursorAsync(string syncPairId, CancellationToken cancellationToken = default)
        {
            return _inner.GetChangeCursorAsync(syncPairId, cancellationToken);
        }

        public virtual Task<SyncStateEntry?> GetAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
        {
            return _inner.GetAsync(syncPairId, relativePath, cancellationToken);
        }

        public virtual Task UpsertAsync(SyncStateEntry entry, CancellationToken cancellationToken = default)
        {
            return _inner.UpsertAsync(entry, cancellationToken);
        }

        public virtual Task SaveChangeCursorAsync(SyncChangeCursor cursor, CancellationToken cancellationToken = default)
        {
            return _inner.SaveChangeCursorAsync(cursor, cancellationToken);
        }

        public virtual Task DeleteAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
        {
            return _inner.DeleteAsync(syncPairId, relativePath, cancellationToken);
        }

        public virtual Task DeletePairAsync(string syncPairId, CancellationToken cancellationToken = default)
        {
            return _inner.DeletePairAsync(syncPairId, cancellationToken);
        }

        public virtual Task ReplacePairAsync(string syncPairId, IReadOnlyCollection<SyncStateEntry> entries, CancellationToken cancellationToken = default)
        {
            return _inner.ReplacePairAsync(syncPairId, entries, cancellationToken);
        }
    }

    private sealed class FailingUpsertStateStore : DelegatingStateStore
    {
        public FailingUpsertStateStore(ISyncStateStore inner)
            : base(inner)
        {
        }

        public override Task UpsertAsync(SyncStateEntry entry, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("State write failed.");
        }
    }

    private sealed class FailingDeleteStateStore : DelegatingStateStore
    {
        public FailingDeleteStateStore(ISyncStateStore inner)
            : base(inner)
        {
        }

        public override Task DeleteAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("State delete failed.");
        }
    }
}
