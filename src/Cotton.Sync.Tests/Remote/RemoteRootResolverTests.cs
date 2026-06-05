// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sdk.Nodes;
using Cotton.Sync.Remote;

namespace Cotton.Sync.Tests.Remote;

public sealed class RemoteRootResolverTests
{
    [Test]
    public async Task EnsureAsync_ReturnsAccountRootForEmptyPath()
    {
        Guid rootId = Guid.NewGuid();
        var client = new FakeNodeClient { Root = Node(rootId, null, "root") };
        var resolver = new RemoteRootResolver(client);

        NodeDto root = await resolver.EnsureAsync();

        Assert.Multiple(() =>
        {
            Assert.That(root.Id, Is.EqualTo(rootId));
            Assert.That(client.CreatedNodes, Is.Empty);
        });
    }

    [Test]
    public async Task EnsureAsync_ReusesExistingDirectoriesCaseInsensitively()
    {
        Guid rootId = Guid.NewGuid();
        Guid docsId = Guid.NewGuid();
        var client = new FakeNodeClient { Root = Node(rootId, null, "root") };
        client.Children[(rootId, 1)] = new NodeContentDto
        {
            TotalCount = 1,
            Nodes = [Node(docsId, rootId, "Docs")],
        };
        var resolver = new RemoteRootResolver(client);

        NodeDto node = await resolver.EnsureAsync("docs");

        Assert.Multiple(() =>
        {
            Assert.That(node.Id, Is.EqualTo(docsId));
            Assert.That(client.CreatedNodes, Is.Empty);
        });
    }

    [Test]
    public async Task EnsureAsync_CreatesMissingNestedDirectoriesAfterPaging()
    {
        Guid rootId = Guid.NewGuid();
        Guid existingId = Guid.NewGuid();
        var client = new FakeNodeClient { Root = Node(rootId, null, "root") };
        client.Children[(rootId, 1)] = new NodeContentDto
        {
            TotalCount = 2,
            Nodes = [Node(existingId, rootId, "Archive")],
        };
        client.Children[(rootId, 2)] = new NodeContentDto
        {
            TotalCount = 2,
            Files = [File(rootId, "skip.txt")],
        };
        var resolver = new RemoteRootResolver(client, pageSize: 1);

        NodeDto created = await resolver.EnsureAsync("Docs/Reports");

        Assert.Multiple(() =>
        {
            Assert.That(client.GetChildrenCalls, Is.EqualTo(new[] { (rootId, 1), (rootId, 2), (client.CreatedNodes[0].Id, 1) }));
            Assert.That(client.CreatedNodes.Select(x => x.Name), Is.EqualTo(new[] { "Docs", "Reports" }));
            Assert.That(created.Id, Is.EqualTo(client.CreatedNodes[1].Id));
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
        };
    }

    private sealed class FakeNodeClient : ICottonNodeClient
    {
        public NodeDto Root { get; set; } = new();

        public Dictionary<(Guid NodeId, int Page), NodeContentDto> Children { get; } = [];

        public List<NodeDto> CreatedNodes { get; } = [];

        public List<(Guid NodeId, int Page)> GetChildrenCalls { get; } = [];

        public Task<NodeDto> ResolveAsync(string? path = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Root);
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
            GetChildrenCalls.Add((nodeId, page));
            return Task.FromResult(Children.TryGetValue((nodeId, page), out NodeContentDto? content)
                ? content
                : new NodeContentDto { TotalCount = 0 });
        }

        public Task<NodeDto> CreateAsync(Guid parentId, string name, CancellationToken cancellationToken = default)
        {
            NodeDto node = Node(Guid.NewGuid(), parentId, name);
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
