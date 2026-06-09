// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sdk.Nodes;
using Cotton.Sync.Remote;

namespace Cotton.Sync.Tests.Remote;

public sealed class RemoteTreeCrawlerTests
{
    [Test]
    public async Task CrawlAsync_WalksPagedFoldersRecursively()
    {
        Guid rootId = Guid.NewGuid();
        Guid docsId = Guid.NewGuid();
        var client = new FakeNodeClient();
        client.Nodes[rootId] = Node(rootId, null, "root");
        client.Nodes[docsId] = Node(docsId, rootId, "Docs");
        client.Children[(rootId, 1)] = new NodeContentDto
        {
            TotalCount = 3,
            Nodes = [client.Nodes[docsId]],
            Files = [File(rootId, "root.txt")],
        };
        client.Children[(rootId, 2)] = new NodeContentDto
        {
            TotalCount = 3,
            Files = [File(rootId, "later.txt")],
        };
        client.Children[(docsId, 1)] = new NodeContentDto
        {
            TotalCount = 1,
            Files = [File(docsId, "report.txt")],
        };
        var crawler = new RemoteTreeCrawler(client, pageSize: 2);

        RemoteTreeSnapshot snapshot = await crawler.CrawlAsync(rootId);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.RootNode.Id, Is.EqualTo(rootId));
            Assert.That(snapshot.Directories.Select(x => x.RelativePath), Is.EqualTo(new[] { "Docs" }));
            Assert.That(snapshot.Files.Select(x => x.RelativePath), Is.EqualTo(new[] { "Docs/report.txt", "later.txt", "root.txt" }));
            Assert.That(client.GetChildrenCalls, Is.EqualTo(new[] { (rootId, 1), (docsId, 1), (rootId, 2) }));
        });
    }

    [Test]
    public async Task CrawlAsync_TraversesFirstPageChildrenBeforeLoadingSiblingPages()
    {
        Guid rootId = Guid.NewGuid();
        Guid docsId = Guid.NewGuid();
        Guid videosId = Guid.NewGuid();
        var client = new FakeNodeClient();
        client.Nodes[rootId] = Node(rootId, null, "root");
        client.Nodes[docsId] = Node(docsId, rootId, "Docs");
        client.Nodes[videosId] = Node(videosId, rootId, "Videos");
        client.Children[(rootId, 1)] = new NodeContentDto
        {
            TotalCount = 2,
            Nodes = [client.Nodes[docsId]],
        };
        client.Children[(rootId, 2)] = new NodeContentDto
        {
            TotalCount = 2,
            Nodes = [client.Nodes[videosId]],
        };
        client.Children[(docsId, 1)] = new NodeContentDto
        {
            TotalCount = 1,
            Files = [File(docsId, "report.txt")],
        };
        client.Children[(videosId, 1)] = new NodeContentDto
        {
            TotalCount = 1,
            Files = [File(videosId, "clip.mp4")],
        };
        var crawler = new RemoteTreeCrawler(client, pageSize: 1);

        RemoteTreeSnapshot snapshot = await crawler.CrawlAsync(rootId);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Directories.Select(x => x.RelativePath), Is.EqualTo(new[] { "Docs", "Videos" }));
            Assert.That(snapshot.Files.Select(x => x.RelativePath), Is.EqualTo(new[] { "Docs/report.txt", "Videos/clip.mp4" }));
            Assert.That(client.GetChildrenCalls, Is.EqualTo(new[] { (rootId, 1), (docsId, 1), (rootId, 2), (videosId, 1) }));
        });
    }

    [Test]
    public async Task CrawlLookupsAsync_ReturnsPathLookups()
    {
        Guid rootId = Guid.NewGuid();
        Guid docsId = Guid.NewGuid();
        var client = new FakeNodeClient();
        client.Nodes[rootId] = Node(rootId, null, "root");
        client.Nodes[docsId] = Node(docsId, rootId, "Docs");
        client.Children[(rootId, 1)] = new NodeContentDto
        {
            TotalCount = 2,
            Nodes = [client.Nodes[docsId]],
            Files = [File(rootId, "root.txt")],
        };
        client.Children[(docsId, 1)] = new NodeContentDto
        {
            TotalCount = 1,
            Files = [File(docsId, "report.txt")],
        };
        var crawler = new RemoteTreeCrawler(client);
        var progress = new RecordingProgress<RemoteTreeScanProgress>();

        RemoteTreeLookupSnapshot snapshot = await crawler.CrawlLookupsAsync(rootId, progress);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.RootNode.Id, Is.EqualTo(rootId));
            Assert.That(snapshot.DirectoriesByPath.Keys, Is.EqualTo(new[] { "DOCS" }));
            Assert.That(snapshot.DirectoriesByPath["DOCS"].RelativePath, Is.EqualTo("Docs"));
            Assert.That(snapshot.FilesByPath.Keys, Is.EqualTo(new[] { "ROOT.TXT", "DOCS/REPORT.TXT" }));
            Assert.That(snapshot.FilesByPath["ROOT.TXT"].RelativePath, Is.EqualTo("root.txt"));
            Assert.That(snapshot.FilesByPath["DOCS/REPORT.TXT"].RelativePath, Is.EqualTo("Docs/report.txt"));
            Assert.That(progress.Values, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(progress.Values[^1].FilesScanned, Is.EqualTo(2));
            Assert.That(progress.Values[^1].DirectoriesScanned, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CrawlAsync_ReturnsEmptySnapshotForEmptyRoot()
    {
        Guid rootId = Guid.NewGuid();
        var client = new FakeNodeClient();
        client.Nodes[rootId] = Node(rootId, null, "root");
        client.Children[(rootId, 1)] = new NodeContentDto { TotalCount = 0 };
        var crawler = new RemoteTreeCrawler(client);

        RemoteTreeSnapshot snapshot = await crawler.CrawlAsync(rootId);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Directories, Is.Empty);
            Assert.That(snapshot.Files, Is.Empty);
        });
    }

    [Test]
    public async Task CrawlAsync_SkipsIgnoredRemoteItems()
    {
        Guid rootId = Guid.NewGuid();
        Guid metadataId = Guid.NewGuid();
        Guid docsId = Guid.NewGuid();
        var client = new FakeNodeClient();
        client.Nodes[rootId] = Node(rootId, null, "root");
        client.Nodes[metadataId] = Node(metadataId, rootId, ".cotton-sync");
        client.Nodes[docsId] = Node(docsId, rootId, "Docs");
        client.Children[(rootId, 1)] = new NodeContentDto
        {
            TotalCount = 4,
            Nodes = [client.Nodes[metadataId], client.Nodes[docsId]],
            Files = [File(rootId, "scratch.tmp"), File(rootId, "keep.txt")],
        };
        client.Children[(metadataId, 1)] = new NodeContentDto
        {
            TotalCount = 1,
            Files = [File(metadataId, "state.sqlite")],
        };
        client.Children[(docsId, 1)] = new NodeContentDto
        {
            TotalCount = 1,
            Files = [File(docsId, "report.txt")],
        };
        var crawler = new RemoteTreeCrawler(client);

        RemoteTreeSnapshot snapshot = await crawler.CrawlAsync(rootId);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Directories.Select(directory => directory.RelativePath), Is.EqualTo(new[] { "Docs" }));
            Assert.That(snapshot.Files.Select(file => file.RelativePath), Is.EqualTo(new[] { "Docs/report.txt", "keep.txt" }));
            Assert.That(client.GetChildrenCalls, Does.Not.Contain((metadataId, 1)));
        });
    }

    [Test]
    public async Task CrawlAsync_ReportsScanProgressAsRemoteFilesAreDiscovered()
    {
        Guid rootId = Guid.NewGuid();
        Guid docsId = Guid.NewGuid();
        var client = new FakeNodeClient();
        client.Nodes[rootId] = Node(rootId, null, "root");
        client.Nodes[docsId] = Node(docsId, rootId, "Docs");
        client.Children[(rootId, 1)] = new NodeContentDto
        {
            TotalCount = 2,
            Nodes = [client.Nodes[docsId]],
            Files = [File(rootId, "root.txt")],
        };
        client.Children[(docsId, 1)] = new NodeContentDto
        {
            TotalCount = 2,
            Files = [File(docsId, "a.txt"), File(docsId, "b.txt")],
        };
        var crawler = new RemoteTreeCrawler(client);
        var progress = new RecordingProgress<RemoteTreeScanProgress>();

        await crawler.CrawlAsync(rootId, progress);

        Assert.Multiple(() =>
        {
            Assert.That(progress.Values, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(progress.Values[0].FilesScanned, Is.Zero);
            Assert.That(progress.Values[0].DirectoriesScanned, Is.Zero);
            Assert.That(progress.Values.Any(item => item.FilesScanned == 1 && item.CurrentPath == "root.txt"), Is.True);
            Assert.That(progress.Values[^1].FilesScanned, Is.EqualTo(3));
            Assert.That(progress.Values[^1].DirectoriesScanned, Is.EqualTo(1));
            Assert.That(progress.Values[^1].CurrentPath, Is.Empty);
        });
    }

    [Test]
    public async Task CrawlAsync_ReportsScanProgressAsRemoteDirectoriesAreDiscovered()
    {
        Guid rootId = Guid.NewGuid();
        Guid docsId = Guid.NewGuid();
        Guid videosId = Guid.NewGuid();
        var client = new FakeNodeClient();
        client.Nodes[rootId] = Node(rootId, null, "root");
        client.Nodes[docsId] = Node(docsId, rootId, "Docs");
        client.Nodes[videosId] = Node(videosId, rootId, "Videos");
        client.Children[(rootId, 1)] = new NodeContentDto
        {
            TotalCount = 2,
            Nodes = [client.Nodes[docsId], client.Nodes[videosId]],
        };
        var crawler = new RemoteTreeCrawler(client);
        var progress = new RecordingProgress<RemoteTreeScanProgress>();

        await crawler.CrawlAsync(rootId, progress);

        Assert.Multiple(() =>
        {
            Assert.That(progress.Values, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(progress.Values.Any(item => item.FilesScanned == 0 && item.DirectoriesScanned == 1 && item.CurrentPath == "Docs"), Is.True);
            Assert.That(progress.Values[^1].FilesScanned, Is.Zero);
            Assert.That(progress.Values[^1].DirectoriesScanned, Is.EqualTo(2));
            Assert.That(progress.Values[^1].CurrentPath, Is.Empty);
        });
    }

    private static NodeDto Node(Guid id, Guid? parentId, string name)
    {
        return new NodeDto
        {
            Id = id,
            ParentId = parentId,
            LayoutId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    private static NodeFileManifestDto File(Guid nodeId, string name)
    {
        return new NodeFileManifestDto
        {
            Id = Guid.NewGuid(),
            NodeId = nodeId,
            FileManifestId = Guid.NewGuid(),
            OriginalNodeFileId = Guid.NewGuid(),
            OwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Name = name,
            ContentType = "text/plain",
            ContentHash = Guid.NewGuid().ToString("N"),
            ETag = "sha256-test",
        };
    }

    private sealed class FakeNodeClient : ICottonNodeClient
    {
        public Dictionary<Guid, NodeDto> Nodes { get; } = [];

        public Dictionary<(Guid NodeId, int Page), NodeContentDto> Children { get; } = [];

        public List<(Guid NodeId, int Page)> GetChildrenCalls { get; } = [];

        public Task<NodeDto> ResolveAsync(string? path = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodeDto> GetAsync(Guid nodeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Nodes[nodeId]);
        }

        public Task<NodeContentDto> GetChildrenAsync(
            Guid nodeId,
            int page = 1,
            int pageSize = 100,
            int depth = 0,
            CancellationToken cancellationToken = default)
        {
            GetChildrenCalls.Add((nodeId, page));
            return Task.FromResult(Children.TryGetValue((nodeId, page), out NodeContentDto? content)
                ? content
                : new NodeContentDto { TotalCount = 0 });
        }

        public Task<NodeDto> CreateAsync(Guid parentId, string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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

        public Task<RestoreOutcomeDto> RestoreAsync(Guid nodeId, RestoreItemRequestDto? request = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<NodeDto>> GetAncestorsAsync(Guid nodeId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value)
        {
            Values.Add(value);
        }
    }
}
