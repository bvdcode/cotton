// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
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
            Assert.That(client.GetChildrenCalls, Is.EqualTo(new[] { (rootId, 1), (rootId, 2), (docsId, 1) }));
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
