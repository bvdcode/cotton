// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Nodes;
using Cotton.Topology;
using EasyExtensions.AspNetCore.Exceptions;

namespace Cotton.Server.IntegrationTests
{
    public class ChildFolderStatsTests : IntegrationTestBase
    {
        [OneTimeSetUp]
        public Task CreateDatabase() => RecreateDatabaseAsync();

        [Test]
        public async Task CountsEachVisibleFileAndAllDescendantsInOneResult()
        {
            Node root = CreateRoot();
            Node first = CreateChild(root, "first");
            Node second = CreateChild(root, "second");
            Node empty = CreateChild(root, "empty");
            Node nested = CreateChild(first, "nested");
            Node deep = CreateChild(nested, "deep");
            FileManifest shared = CreateFile(first, 13).FileManifest;
            CreateFile(second, 13, shared);
            CreateFile(deep, 7);
            CreateFile(root, 999);
            Node wrongType = CreateChild(first, "trash");
            wrongType.Type = NodeType.Trash;
            CreateFile(wrongType, 500);
            Node otherRoot = CreateRoot();
            CreateFile(CreateChild(otherRoot, "unrelated"), 900);
            await DbContext.SaveChangesAsync();

            Dictionary<Guid, FolderStatsDto> result = await Handler().Handle(new(root.OwnerId, root.Id, root.Type), CancellationToken.None);
            Assert.That(result.Keys, Is.EquivalentTo(new[] { first.Id, second.Id, empty.Id }));
            Assert.Multiple(() =>
            {
                Assert.That(result[first.Id].SizeBytes, Is.EqualTo(20));
                Assert.That(result[first.Id].Files, Is.EqualTo(2));
                Assert.That(result[first.Id].Folders, Is.EqualTo(2));
                Assert.That(result[second.Id].SizeBytes, Is.EqualTo(13));
                Assert.That(result[empty.Id].SizeBytes, Is.Zero);
                Assert.That(result[empty.Id].Files, Is.Zero);
            });
            GetFolderStatsQueryHandler original = new(DbContext);
            foreach (Guid id in result.Keys)
            {
                FolderStatsDto expected = (await original.Handle(new(root.OwnerId, id, true), CancellationToken.None))!;
                Assert.That(result[id], Is.EqualTo(expected).UsingPropertiesComparer());
            }
        }

        [Test]
        public async Task UsesSameTrashDepthAndVersionFilterAsListing()
        {
            Node root = CreateRoot(NodeType.Trash);
            Node wrapper = CreateChild(root, "wrapper");
            Node deleted = CreateChild(wrapper, "deleted");
            Node nested = CreateChild(deleted, "nested");
            CreateFile(nested, 42);
            Node versionWrapper = CreateChild(root, "versions");
            NodeFile version = CreateFile(versionWrapper, 900);
            version.OriginalNodeFileId = Guid.NewGuid();
            await DbContext.SaveChangesAsync();

            foreach (int depth in new[] { 0, 1 })
            {
                Dictionary<Guid, FolderStatsDto> result = await Handler().Handle(new(root.OwnerId, root.Id, root.Type, depth), CancellationToken.None);
                GetChildrenQueryHandler listing = new(new StorageLayoutService(DbContext), DbContext);
                var page = await listing.Handle(new(root.OwnerId, root.Id, root.Type, 1, 25000, depth), CancellationToken.None);
                Assert.That(result.Keys, Is.EquivalentTo(page.Payload.Nodes.Select(node => node.Id)));
                Assert.That(result.Values.Sum(stats => stats.SizeBytes), Is.EqualTo(42));
            }
        }

        [Test]
        public async Task RejectsOtherOwnersWrongTypesAndInactiveLayouts()
        {
            Node root = CreateRoot();
            Node other = CreateRoot();
            await DbContext.SaveChangesAsync();
            await Assert.ThrowsAsync<EntityNotFoundException>(() => Handler().Handle(new(other.OwnerId, root.Id, root.Type), CancellationToken.None));
            await Assert.ThrowsAsync<EntityNotFoundException>(() => Handler().Handle(new(root.OwnerId, root.Id, NodeType.Trash), CancellationToken.None));
            root.Layout.IsActive = false;
            DbContext.UserLayouts.Add(new Layout { Owner = root.Owner, IsActive = true });
            await DbContext.SaveChangesAsync();
            await Assert.ThrowsAsync<EntityNotFoundException>(() => Handler().Handle(new(root.OwnerId, root.Id, root.Type), CancellationToken.None));
        }

        [Test]
        public async Task EmptyDirectoriesReturnNoSizes()
        {
            Node root = CreateRoot();
            CreateFile(root, 12);
            await DbContext.SaveChangesAsync();
            Dictionary<Guid, FolderStatsDto> result = await Handler().Handle(new(root.OwnerId, root.Id, root.Type), CancellationToken.None);
            Assert.That(result, Is.Empty);
        }

        [TestCase(NodeType.Default, 0)]
        [TestCase(NodeType.Default, 1)]
        [TestCase(NodeType.Trash, 0)]
        [TestCase(NodeType.Trash, 1)]
        public async Task ListingIncludesRequestedNodeMetadataEvenOnEmptyPages(NodeType type, int depth)
        {
            Node root = CreateRoot(type);
            root.Metadata = new Dictionary<string, string> { ["color"] = "blue" };
            await DbContext.SaveChangesAsync();
            await DbContext.Entry(root).ReloadAsync();
            GetChildrenQueryHandler listing = new(new StorageLayoutService(DbContext), DbContext);
            var empty = await listing.Handle(new(root.OwnerId, root.Id, type, 1, 100, depth), CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(empty.Payload.Node.Id, Is.EqualTo(root.Id));
                Assert.That(empty.Payload.Node.LayoutId, Is.EqualTo(root.LayoutId));
                Assert.That(empty.Payload.Node.ParentId, Is.EqualTo(root.ParentId));
                Assert.That(empty.Payload.Node.Name, Is.EqualTo(root.Name));
                Assert.That(empty.Payload.Node.CreatedAt, Is.EqualTo(root.CreatedAt));
                Assert.That(empty.Payload.Node.UpdatedAt, Is.EqualTo(root.UpdatedAt));
                Assert.That(empty.Payload.Node.Metadata["color"], Is.EqualTo("blue"));
            });
            Assert.That(empty.Payload.Nodes, Is.Empty);

            CreateChild(root, "child");
            root.Metadata["color"] = "green";
            await DbContext.SaveChangesAsync();
            var populated = await listing.Handle(new(root.OwnerId, root.Id, type, 1, 100, depth), CancellationToken.None);
            var beyondLastPage = await listing.Handle(new(root.OwnerId, root.Id, type, 2, 100, depth), CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(populated.Payload.Node.Id, Is.EqualTo(root.Id));
                Assert.That(populated.Payload.Node.Metadata["color"], Is.EqualTo("green"));
                Assert.That(beyondLastPage.Payload.Node.Id, Is.EqualTo(root.Id));
                Assert.That(beyondLastPage.Payload.Node.Metadata["color"], Is.EqualTo("green"));
                Assert.That(beyondLastPage.Payload.Nodes, Is.Empty);
            });
        }

        [TestCase(-1)]
        [TestCase(33)]
        public async Task RejectsInvalidDepth(int depth)
        {
            await Assert.ThrowsAsync<BadRequestException>(() => Handler().Handle(new(Guid.NewGuid(), Guid.NewGuid(), NodeType.Default, depth), CancellationToken.None));
        }

        [Test]
        public async Task HonorsRequestCancellation()
        {
            Node root = CreateRoot();
            await DbContext.SaveChangesAsync();
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();
            await Assert.CatchAsync<OperationCanceledException>(() => Handler().Handle(new(root.OwnerId, root.Id, root.Type), cancellation.Token));
        }

        private GetChildFolderStatsQueryHandler Handler() => new(DbContext, new StorageLayoutService(DbContext));

        private Node CreateRoot(NodeType type = NodeType.Default)
        {
            User user = new() { Username = Guid.NewGuid().ToString("N"), PasswordPhc = "test", WebDavTokenPhc = "test" };
            Layout layout = new() { Owner = user, IsActive = true };
            Node root = new() { Owner = user, Layout = layout, Type = type };
            root.SetName("root");
            DbContext.Nodes.Add(root);
            return root;
        }

        private Node CreateChild(Node parent, string name)
        {
            Node child = new() { Owner = parent.Owner, Layout = parent.Layout, Type = parent.Type, ParentId = parent.Id };
            child.SetName(name);
            DbContext.Nodes.Add(child);
            return child;
        }

        private NodeFile CreateFile(Node parent, long bytes, FileManifest? manifest = null)
        {
            NodeFile file = new()
            {
                Owner = parent.Owner,
                Node = parent,
                FileManifest = manifest ?? new FileManifest
                {
                    SizeBytes = bytes,
                    ProposedContentHash = System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray()),
                    ContentType = "application/octet-stream",
                },
            };
            file.SetName(Guid.NewGuid().ToString("N"));
            file.OriginalNodeFileId = file.Id;
            DbContext.NodeFiles.Add(file);
            return file;
        }
    }
}
