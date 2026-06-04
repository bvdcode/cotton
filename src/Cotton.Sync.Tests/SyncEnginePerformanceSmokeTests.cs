// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;

namespace Cotton.Sync.Tests;

public sealed class SyncEnginePerformanceSmokeTests
{
    private static readonly Guid RemoteRootNodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private string _root = string.Empty;
    private string _databasePath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "cotton-sync-performance", Guid.NewGuid().ToString("N"));
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
    public async Task RunOnceAsync_NoOpForOneThousandFilesCompletesWithinSmokeTarget()
    {
        const int fileCount = 1_000;
        TimeSpan smokeTarget = TimeSpan.FromSeconds(20);
        SqliteSyncStateStore stateStore = new(_databasePath);
        await stateStore.InitializeAsync();
        List<RemoteFileSnapshot> remoteFiles = [];
        List<SyncStateEntry> baselineEntries = [];

        for (int index = 0; index < fileCount; index++)
        {
            string relativePath = $"Docs/{index / 100:D2}/file-{index:D4}.txt";
            byte[] content = Encoding.UTF8.GetBytes("content-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
            string hash = Hash(content);
            WriteFile(relativePath, content);
            NodeFileManifestDto remoteFile = RemoteFile(relativePath, hash, content.Length);
            remoteFiles.Add(new RemoteFileSnapshot
            {
                RelativePath = relativePath,
                File = remoteFile,
            });
            baselineEntries.Add(new SyncStateEntry
            {
                SyncPairId = "performance-noop",
                RelativePath = relativePath,
                Kind = SyncEntryKind.File,
                LocalContentHash = hash,
                LocalLastWriteUtc = File.GetLastWriteTimeUtc(FullPath(relativePath)),
                RemoteNodeId = remoteFile.NodeId,
                RemoteFileId = remoteFile.Id,
                RemoteContentHash = remoteFile.ContentHash,
                RemoteETag = remoteFile.ETag,
                SyncedAtUtc = DateTime.UtcNow,
            });
        }

        await stateStore.ReplacePairAsync("performance-noop", baselineEntries);

        var remoteFilesClient = new GuardedRemoteFileSynchronizer();
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new StaticRemoteTreeCrawler(remoteFiles),
            remoteFilesClient,
            stateStore);

        Stopwatch stopwatch = Stopwatch.StartNew();
        SyncRunResult result = await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "performance-noop",
            LocalRootPath = _root,
            RemoteRootNodeId = RemoteRootNodeId,
        });
        stopwatch.Stop();

        IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync("performance-noop");
        TestContext.WriteLine(
            "No-op sync smoke for {0} files completed in {1:N0} ms.",
            fileCount,
            stopwatch.Elapsed.TotalMilliseconds);

        Assert.Multiple(() =>
        {
            Assert.That(result.Activities, Is.Empty);
            Assert.That(remoteFilesClient.UploadCalls, Is.Zero);
            Assert.That(remoteFilesClient.DownloadCalls, Is.Zero);
            Assert.That(remoteFilesClient.DeleteCalls, Is.Zero);
            Assert.That(baselines, Has.Count.EqualTo(fileCount));
            Assert.That(stopwatch.Elapsed, Is.LessThan(smokeTarget));
        });
    }

    [Test]
    public async Task RunOnceAsync_UploadsOneThousandSmallFilesWithinSmokeTarget()
    {
        const int fileCount = 1_000;
        TimeSpan smokeTarget = TimeSpan.FromSeconds(30);
        for (int index = 0; index < fileCount; index++)
        {
            string relativePath = $"Upload/{index / 100:D2}/small-{index:D4}.txt";
            byte[] content = Encoding.UTF8.GetBytes("small-upload-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
            WriteFile(relativePath, content);
        }

        SqliteSyncStateStore stateStore = new(_databasePath);
        var remoteFilesClient = new RecordingRemoteFileSynchronizer();
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new StaticRemoteTreeCrawler([]),
            remoteFilesClient,
            stateStore);

        Stopwatch stopwatch = Stopwatch.StartNew();
        SyncRunResult result = await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "performance-upload-small",
            LocalRootPath = _root,
            RemoteRootNodeId = RemoteRootNodeId,
        });
        stopwatch.Stop();

        IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync("performance-upload-small");
        int distinctRemoteFileIds = baselines
            .Select(entry => entry.RemoteFileId)
            .Where(id => id.HasValue)
            .Distinct()
            .Count();
        TestContext.WriteLine(
            "Initial upload smoke for {0} small files completed in {1:N0} ms.",
            fileCount,
            stopwatch.Elapsed.TotalMilliseconds);

        Assert.Multiple(() =>
        {
            Assert.That(remoteFilesClient.UploadCalls, Is.EqualTo(fileCount));
            Assert.That(remoteFilesClient.DownloadCalls, Is.Zero);
            Assert.That(remoteFilesClient.DeleteCalls, Is.Zero);
            Assert.That(result.Activities, Has.Count.EqualTo(fileCount));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.All.EqualTo(SyncActivityKind.Uploaded));
            Assert.That(baselines, Has.Count.EqualTo(fileCount));
            Assert.That(distinctRemoteFileIds, Is.EqualTo(fileCount));
            Assert.That(stopwatch.Elapsed, Is.LessThan(smokeTarget));
        });
    }

    [Test]
    public async Task RunOnceAsync_UploadsOneLargeFileWithinSmokeTarget()
    {
        const int fileSizeBytes = 8 * 1024 * 1024;
        const string relativePath = "Large/single-large.bin";
        TimeSpan smokeTarget = TimeSpan.FromSeconds(15);
        byte[] content = CreateDeterministicBytes(fileSizeBytes);
        string expectedHash = Hash(content);
        WriteFile(relativePath, content);
        SqliteSyncStateStore stateStore = new(_databasePath);
        var remoteFilesClient = new RecordingRemoteFileSynchronizer();
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new StaticRemoteTreeCrawler([]),
            remoteFilesClient,
            stateStore);

        Stopwatch stopwatch = Stopwatch.StartNew();
        SyncRunResult result = await engine.RunOnceAsync(new SyncPair
        {
            SyncPairId = "performance-upload-large",
            LocalRootPath = _root,
            RemoteRootNodeId = RemoteRootNodeId,
        });
        stopwatch.Stop();

        SyncStateEntry? baseline = await stateStore.GetAsync("performance-upload-large", relativePath);
        TestContext.WriteLine(
            "Initial upload smoke for one {0:N0}-byte file completed in {1:N0} ms.",
            fileSizeBytes,
            stopwatch.Elapsed.TotalMilliseconds);

        Assert.Multiple(() =>
        {
            Assert.That(remoteFilesClient.UploadCalls, Is.EqualTo(1));
            Assert.That(remoteFilesClient.DownloadCalls, Is.Zero);
            Assert.That(remoteFilesClient.DeleteCalls, Is.Zero);
            Assert.That(remoteFilesClient.Uploads.Single().RelativePath, Is.EqualTo(relativePath));
            Assert.That(remoteFilesClient.Uploads.Single().LocalFile.SizeBytes, Is.EqualTo(fileSizeBytes));
            Assert.That(remoteFilesClient.Uploads.Single().LocalFile.ContentHash, Is.EqualTo(expectedHash));
            Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
            Assert.That(baseline, Is.Not.Null);
            Assert.That(baseline!.LocalContentHash, Is.EqualTo(expectedHash));
            Assert.That(baseline.RemoteContentHash, Is.EqualTo(expectedHash));
            Assert.That(stopwatch.Elapsed, Is.LessThan(smokeTarget));
        });
    }

    private string FullPath(string relativePath)
    {
        return Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private void WriteFile(string relativePath, byte[] content)
    {
        string fullPath = FullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        File.SetLastWriteTimeUtc(fullPath, new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc));
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static byte[] CreateDeterministicBytes(int length)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)((index * 31 + index / 17) % 251);
        }

        return bytes;
    }

    private static NodeFileManifestDto RemoteFile(string relativePath, string contentHash, long sizeBytes)
    {
        return new NodeFileManifestDto
        {
            Id = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc),
            NodeId = RemoteRootNodeId,
            FileManifestId = Guid.NewGuid(),
            OriginalNodeFileId = Guid.NewGuid(),
            OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = relativePath.Split('/')[^1],
            ContentType = "text/plain",
            SizeBytes = sizeBytes,
            ContentHash = contentHash,
            ETag = "sha256-" + contentHash,
            Metadata = new Dictionary<string, string> { ["relativePath"] = relativePath },
        };
    }

    private sealed class StaticRemoteTreeCrawler : IRemoteTreeCrawler
    {
        private readonly IReadOnlyList<RemoteFileSnapshot> _files;

        public StaticRemoteTreeCrawler(IReadOnlyList<RemoteFileSnapshot> files)
        {
            _files = files;
        }

        public Task<RemoteTreeSnapshot> CrawlAsync(Guid rootNodeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RemoteTreeSnapshot
            {
                RootNode = new NodeDto
                {
                    Id = rootNodeId,
                    Name = "root",
                },
                Files = _files.ToList(),
            });
        }
    }

    private sealed class GuardedRemoteFileSynchronizer : IRemoteFileSynchronizer
    {
        public int UploadCalls { get; private set; }

        public int DownloadCalls { get; private set; }

        public int DeleteCalls { get; private set; }

        public Task<NodeFileManifestDto> UploadFileAsync(
            Guid rootNodeId,
            string relativePath,
            LocalFileSnapshot localFile,
            NodeFileManifestDto? existingRemoteFile = null,
            CancellationToken cancellationToken = default)
        {
            UploadCalls++;
            throw new InvalidOperationException("No-op performance smoke must not upload files.");
        }

        public Task DownloadFileAsync(Guid nodeFileId, Stream destination, CancellationToken cancellationToken = default)
        {
            DownloadCalls++;
            throw new InvalidOperationException("No-op performance smoke must not download files.");
        }

        public Task DeleteFileAsync(
            Guid nodeFileId,
            bool skipTrash = false,
            string? expectedETag = null,
            CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            throw new InvalidOperationException("No-op performance smoke must not delete files.");
        }
    }

    private sealed class RecordingRemoteFileSynchronizer : IRemoteFileSynchronizer
    {
        public List<UploadCall> Uploads { get; } = [];

        public int UploadCalls { get; private set; }

        public int DownloadCalls { get; private set; }

        public int DeleteCalls { get; private set; }

        public Task<NodeFileManifestDto> UploadFileAsync(
            Guid rootNodeId,
            string relativePath,
            LocalFileSnapshot localFile,
            NodeFileManifestDto? existingRemoteFile = null,
            CancellationToken cancellationToken = default)
        {
            UploadCalls++;
            NodeFileManifestDto uploaded = RemoteFile(relativePath, localFile.ContentHash, localFile.SizeBytes);
            uploaded.Id = existingRemoteFile?.Id ?? uploaded.Id;
            uploaded.NodeId = existingRemoteFile?.NodeId ?? rootNodeId;
            uploaded.UpdatedAt = localFile.LastWriteUtc;
            Uploads.Add(new UploadCall(rootNodeId, relativePath, localFile, existingRemoteFile, uploaded));
            return Task.FromResult(uploaded);
        }

        public Task DownloadFileAsync(Guid nodeFileId, Stream destination, CancellationToken cancellationToken = default)
        {
            DownloadCalls++;
            throw new InvalidOperationException("Initial upload performance smoke must not download files.");
        }

        public Task DeleteFileAsync(
            Guid nodeFileId,
            bool skipTrash = false,
            string? expectedETag = null,
            CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            throw new InvalidOperationException("Initial upload performance smoke must not delete files.");
        }
    }

    private sealed record UploadCall(
        Guid RootNodeId,
        string RelativePath,
        LocalFileSnapshot LocalFile,
        NodeFileManifestDto? ExistingRemoteFile,
        NodeFileManifestDto ReturnedFile);
}
