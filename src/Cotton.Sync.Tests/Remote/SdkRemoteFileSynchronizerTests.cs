// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;
using Cotton.Contracts.Auth;
using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
using Cotton.Contracts.Settings;
using Cotton.Sdk;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Chunks;
using Cotton.Sdk.Files;
using Cotton.Sdk.Nodes;
using Cotton.Sdk.Realtime;
using Cotton.Sdk.Settings;
using Cotton.Sdk.Sync;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;

namespace Cotton.Sync.Tests.Remote;

public sealed class SdkRemoteFileSynchronizerTests
{
    private readonly Guid _rootNodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "cotton-sdk-remote-sync", Guid.NewGuid().ToString("N"));
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
    public async Task UploadFileAsync_CreatesFoldersUploadsMissingChunksAndCreatesFile()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("abcdefghij");
        LocalFileSnapshot local = WriteLocalFile("Docs/Reports/file.txt", bytes);
        var client = new FakeCottonCloudClient(chunkSizeBytes: 4);
        string firstChunkHash = Hash(Encoding.UTF8.GetBytes("abcd"));
        client.ChunksClient.ExistingHashes.Add(firstChunkHash);
        var synchronizer = new SdkRemoteFileSynchronizer(client);

        NodeFileManifestDto created = await synchronizer.UploadFileAsync(_rootNodeId, local.RelativePath, local);

        Assert.Multiple(() =>
        {
            Assert.That(client.SettingsClient.Calls, Is.EqualTo(1));
            Assert.That(client.NodesClient.CreatedNodes.Select(x => x.Name), Is.EqualTo(new[] { "Docs", "Reports" }));
            Assert.That(client.ChunksClient.ExistsChecks, Has.Count.EqualTo(3));
            Assert.That(client.ChunksClient.UploadedChunks.Select(x => x.Hash), Is.EqualTo(client.ChunksClient.ExistsChecks.Skip(1)));
            Assert.That(client.FilesClient.CreateRequests, Has.Count.EqualTo(1));
            Assert.That(client.FilesClient.UpdateRequests, Is.Empty);
            Assert.That(client.FilesClient.CreateRequests[0].NodeId, Is.EqualTo(client.NodesClient.CreatedNodes[^1].Id));
            Assert.That(client.FilesClient.CreateRequests[0].Name, Is.EqualTo("file.txt"));
            Assert.That(client.FilesClient.CreateRequests[0].ContentType, Is.EqualTo("text/plain"));
            Assert.That(client.FilesClient.CreateRequests[0].Hash, Is.EqualTo(local.ContentHash));
            Assert.That(client.FilesClient.CreateRequests[0].Validate, Is.True);
            Assert.That(created.ContentHash, Is.EqualTo(local.ContentHash));
        });
    }

    [Test]
    public async Task UploadFileAsync_ReusesExistingFolderAndUpdatesExistingFile()
    {
        Guid docsId = Guid.NewGuid();
        byte[] bytes = Encoding.UTF8.GetBytes("updated");
        LocalFileSnapshot local = WriteLocalFile("Docs/file.bin", bytes);
        var client = new FakeCottonCloudClient(chunkSizeBytes: 1024);
        client.NodesClient.Children[_rootNodeId] = [Node(docsId, _rootNodeId, "Docs")];
        NodeFileManifestDto existing = RemoteFile("file.bin", HashText("old"));
        var synchronizer = new SdkRemoteFileSynchronizer(client);

        NodeFileManifestDto updated = await synchronizer.UploadFileAsync(_rootNodeId, local.RelativePath, local, existing);

        Assert.Multiple(() =>
        {
            Assert.That(client.NodesClient.CreatedNodes, Is.Empty);
            Assert.That(client.FilesClient.CreateRequests, Is.Empty);
            Assert.That(client.FilesClient.UpdateRequests, Has.Count.EqualTo(1));
            Assert.That(client.FilesClient.UpdateRequests[0].NodeFileId, Is.EqualTo(existing.Id));
            Assert.That(client.FilesClient.UpdateRequests[0].Request.NodeId, Is.EqualTo(docsId));
            Assert.That(client.FilesClient.UpdateRequests[0].Request.OriginalNodeFileId, Is.EqualTo(existing.OriginalNodeFileId));
            Assert.That(client.FilesClient.UpdateRequests[0].ExpectedETag, Is.EqualTo(existing.ETag));
            Assert.That(updated.Id, Is.EqualTo(existing.Id));
            Assert.That(updated.ContentHash, Is.EqualTo(local.ContentHash));
        });
    }

    [Test]
    public async Task UploadFileAsync_UploadsEmptyFileAsEmptyChunk()
    {
        LocalFileSnapshot local = WriteLocalFile("empty.bin", []);
        var client = new FakeCottonCloudClient(chunkSizeBytes: 8);
        var synchronizer = new SdkRemoteFileSynchronizer(client);

        await synchronizer.UploadFileAsync(_rootNodeId, local.RelativePath, local);

        string emptyHash = Hash([]);
        Assert.Multiple(() =>
        {
            Assert.That(client.ChunksClient.ExistsChecks, Is.EqualTo(new[] { emptyHash }));
            Assert.That(client.ChunksClient.UploadedChunks, Has.Count.EqualTo(1));
            Assert.That(client.ChunksClient.UploadedChunks[0].Bytes, Is.Empty);
            Assert.That(client.FilesClient.CreateRequests[0].ChunkHashes, Is.EqualTo(new[] { emptyHash }));
        });
    }

    [Test]
    public async Task UploadFileAsync_ReportsChunkProgress()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("abcdefghij");
        LocalFileSnapshot local = WriteLocalFile("Docs/file.txt", bytes);
        var client = new FakeCottonCloudClient(chunkSizeBytes: 4);
        var synchronizer = new SdkRemoteFileSynchronizer(client);
        var progress = new RecordingProgress<SyncTransferProgress>();

        await synchronizer.UploadFileAsync(
            _rootNodeId,
            local.RelativePath,
            local,
            existingRemoteFile: null,
            transferProgress: progress);

        Assert.Multiple(() =>
        {
            Assert.That(progress.Values.Select(value => value.TransferredBytes), Is.EqualTo(new long[] { 0, 4, 8, 10, 10 }));
            Assert.That(progress.Values.Select(value => value.TotalBytes), Is.All.EqualTo(10));
            Assert.That(progress.Values.Select(value => value.Direction), Is.All.EqualTo(SyncTransferDirection.Upload));
            Assert.That(progress.Values.Select(value => value.RelativePath), Is.All.EqualTo("Docs/file.txt"));
            Assert.That(progress.Values[^1].IsCompleted, Is.True);
        });
    }

    [Test]
    public async Task DownloadFileAsync_And_DeleteFileAsync_DelegateToSdkFileClient()
    {
        Guid fileId = Guid.NewGuid();
        var client = new FakeCottonCloudClient(chunkSizeBytes: 8);
        client.FilesClient.Downloads[fileId] = Encoding.UTF8.GetBytes("downloaded");
        var synchronizer = new SdkRemoteFileSynchronizer(client);
        await using var destination = new MemoryStream();

        await synchronizer.DownloadFileAsync(fileId, destination);
        await synchronizer.DeleteFileAsync(fileId, skipTrash: true, expectedETag: "sha256-current");

        Assert.Multiple(() =>
        {
            Assert.That(Encoding.UTF8.GetString(destination.ToArray()), Is.EqualTo("downloaded"));
            Assert.That(client.FilesClient.Deletes, Is.EqualTo(new[] { (fileId, true, "sha256-current") }));
        });
    }

    [Test]
    public async Task DownloadFileAsync_ReportsSdkDownloadProgress()
    {
        Guid fileId = Guid.NewGuid();
        var client = new FakeCottonCloudClient(chunkSizeBytes: 8);
        client.FilesClient.Downloads[fileId] = Encoding.UTF8.GetBytes("downloaded");
        var synchronizer = new SdkRemoteFileSynchronizer(client);
        await using var destination = new MemoryStream();
        var progress = new RecordingProgress<SyncTransferProgress>();

        await synchronizer.DownloadFileAsync(
            fileId,
            "Docs/file.txt",
            totalBytes: 10,
            destination,
            progress);

        Assert.Multiple(() =>
        {
            Assert.That(progress.Values.Select(value => value.TransferredBytes), Is.EqualTo(new long[] { 0, 10, 10 }));
            Assert.That(progress.Values.Select(value => value.TotalBytes), Is.All.EqualTo(10));
            Assert.That(progress.Values.Select(value => value.Direction), Is.All.EqualTo(SyncTransferDirection.Download));
            Assert.That(progress.Values.Select(value => value.RelativePath), Is.All.EqualTo("Docs/file.txt"));
            Assert.That(progress.Values[^1].IsCompleted, Is.True);
        });
    }

    private LocalFileSnapshot WriteLocalFile(string relativePath, byte[] bytes)
    {
        string fullPath = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes);
        File.SetLastWriteTimeUtc(fullPath, new DateTime(2026, 6, 2, 13, 0, 0, DateTimeKind.Utc));
        return new LocalFileSnapshot
        {
            RelativePath = relativePath,
            FullPath = fullPath,
            ContentHash = Hash(bytes),
            SizeBytes = bytes.Length,
            LastWriteUtc = new DateTime(2026, 6, 2, 13, 0, 0, DateTimeKind.Utc),
        };
    }

    private NodeFileManifestDto RemoteFile(string name, string contentHash)
    {
        return new NodeFileManifestDto
        {
            Id = Guid.NewGuid(),
            NodeId = _rootNodeId,
            FileManifestId = Guid.NewGuid(),
            OriginalNodeFileId = Guid.NewGuid(),
            OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = name,
            ContentType = "application/octet-stream",
            ContentHash = contentHash,
            ETag = "sha256-" + contentHash,
        };
    }

    private static NodeDto Node(Guid id, Guid parentId, string name)
    {
        return new NodeDto
        {
            Id = id,
            ParentId = parentId,
            LayoutId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Name = name,
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

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value)
        {
            Values.Add(value);
        }
    }

    private sealed class FakeCottonCloudClient : ICottonCloudClient
    {
        public FakeCottonCloudClient(int chunkSizeBytes)
        {
            SettingsClient = new FakeSettingsClient(chunkSizeBytes);
        }

        public ICottonAuthClient Auth => throw new NotSupportedException();

        public FakeSettingsClient SettingsClient { get; }

        public FakeChunkClient ChunksClient { get; } = new();

        public FakeFileClient FilesClient { get; } = new();

        public FakeNodeClient NodesClient { get; } = new();

        public ICottonSettingsClient Settings => SettingsClient;

        public ICottonChunkClient Chunks => ChunksClient;

        public ICottonFileClient Files => FilesClient;

        public ICottonNodeClient Nodes => NodesClient;

        public ICottonSyncClient Sync => throw new NotSupportedException();

        public ICottonRealtimeClient Realtime => throw new NotSupportedException();
    }

    private sealed class FakeSettingsClient : ICottonSettingsClient
    {
        private readonly int _chunkSizeBytes;

        public FakeSettingsClient(int chunkSizeBytes)
        {
            _chunkSizeBytes = chunkSizeBytes;
        }

        public int Calls { get; private set; }

        public Task<ClientSettingsDto> GetAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ClientSettingsDto
            {
                MaxChunkSizeBytes = _chunkSizeBytes,
                SupportedHashAlgorithm = "SHA-256",
            });
        }
    }

    private sealed class FakeChunkClient : ICottonChunkClient
    {
        public HashSet<string> ExistingHashes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> ExistsChecks { get; } = [];

        public List<(string Hash, byte[] Bytes)> UploadedChunks { get; } = [];

        public Task<bool> ExistsAsync(string hash, CancellationToken cancellationToken = default)
        {
            ExistsChecks.Add(hash);
            return Task.FromResult(ExistingHashes.Contains(hash));
        }

        public async Task UploadRawAsync(
            string hash,
            Stream content,
            string contentType = "application/octet-stream",
            CancellationToken cancellationToken = default)
        {
            await using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            UploadedChunks.Add((hash, copy.ToArray()));
            ExistingHashes.Add(hash);
        }
    }

    private sealed class FakeFileClient : ICottonFileClient
    {
        public List<CreateFileFromChunksRequestDto> CreateRequests { get; } = [];

        public List<(Guid NodeFileId, CreateFileFromChunksRequestDto Request, string? ExpectedETag)> UpdateRequests { get; } = [];

        public List<(Guid NodeFileId, bool SkipTrash, string? ExpectedETag)> Deletes { get; } = [];

        public Dictionary<Guid, byte[]> Downloads { get; } = [];

        public Task<NodeFileManifestDto> CreateFromChunksAsync(
            CreateFileFromChunksRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CreateRequests.Add(request);
            return Task.FromResult(FileFromRequest(Guid.NewGuid(), request));
        }

        public Task<NodeFileManifestDto> UpdateContentAsync(
            Guid nodeFileId,
            CreateFileFromChunksRequestDto request,
            string? expectedETag = null,
            CancellationToken cancellationToken = default)
        {
            UpdateRequests.Add((nodeFileId, request, expectedETag));
            return Task.FromResult(FileFromRequest(nodeFileId, request));
        }

        public Task<NodeFileManifestDto> MoveAsync(
            Guid nodeFileId,
            Guid parentId,
            string? expectedETag = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeFileManifestDto> RenameAsync(
            Guid nodeFileId,
            string name,
            string? expectedETag = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeFileManifestDto> UpdateMetadataAsync(
            Guid nodeFileId,
            IReadOnlyDictionary<string, string> metadata,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(
            Guid nodeFileId,
            bool skipTrash = false,
            string? expectedETag = null,
            CancellationToken cancellationToken = default)
        {
            Deletes.Add((nodeFileId, skipTrash, expectedETag));
            return Task.CompletedTask;
        }

        public Task<NodeFileManifestDto> RestoreAsync(
            Guid nodeFileId,
            RestoreItemRequestDto? request = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<FileVersionDto>> GetVersionsAsync(Guid nodeFileId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public async Task DownloadContentAsync(
            Guid nodeFileId,
            Stream destination,
            bool download = false,
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default)
        {
            byte[] bytes = Downloads[nodeFileId];
            await destination.WriteAsync(bytes, cancellationToken);
            progress?.Report(bytes.Length);
        }

        private static NodeFileManifestDto FileFromRequest(Guid id, CreateFileFromChunksRequestDto request)
        {
            return new NodeFileManifestDto
            {
                Id = id,
                NodeId = request.NodeId,
                FileManifestId = Guid.NewGuid(),
                OriginalNodeFileId = request.OriginalNodeFileId ?? id,
                OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = request.Name,
                ContentType = request.ContentType,
                ContentHash = request.Hash,
                ETag = "sha256-" + request.Hash,
            };
        }
    }

    private sealed class FakeNodeClient : ICottonNodeClient
    {
        public Dictionary<Guid, List<NodeDto>> Children { get; } = [];

        public List<NodeDto> CreatedNodes { get; } = [];

        public Task<NodeDto> ResolveAsync(string? path = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> GetAsync(Guid nodeId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeContentDto> GetChildrenAsync(
            Guid nodeId,
            int page = 1,
            int pageSize = 100,
            int depth = 0,
            CancellationToken cancellationToken = default)
        {
            List<NodeDto> allChildren = Children.TryGetValue(nodeId, out List<NodeDto>? children) ? children : [];
            List<NodeDto> nodes = allChildren.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new NodeContentDto
            {
                TotalCount = allChildren.Count,
                Nodes = nodes,
            });
        }

        public Task<NodeDto> CreateAsync(Guid parentId, string name, CancellationToken cancellationToken = default)
        {
            NodeDto node = Node(Guid.NewGuid(), parentId, name);
            if (!Children.TryGetValue(parentId, out List<NodeDto>? children))
            {
                children = [];
                Children[parentId] = children;
            }

            children.Add(node);
            CreatedNodes.Add(node);
            return Task.FromResult(node);
        }

        public Task<NodeDto> MoveAsync(Guid nodeId, Guid parentId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> RenameAsync(Guid nodeId, string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> UpdateMetadataAsync(Guid nodeId, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid nodeId, bool skipTrash = false, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> RestoreAsync(RestoreItemRequestDto? request = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> RestoreAsync(Guid nodeId, RestoreItemRequestDto? request = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<NodeDto>> GetAncestorsAsync(Guid nodeId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
