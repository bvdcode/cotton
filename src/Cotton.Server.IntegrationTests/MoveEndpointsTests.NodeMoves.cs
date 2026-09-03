// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class MoveEndpointsTests
    {
        // ---------------------------------------------------------------------
        // MoveNode
        // ---------------------------------------------------------------------

        [Test]
        public async Task MoveNode_ToAnotherFolder_Succeeds()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src = await CreateFolderAsync(root.Id, "src");
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");
            NodeDto moving = await CreateFolderAsync(src.Id, "moving");

            HttpResponseMessage res = await MoveNodeAsync(moving.Id, dst.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            NodeContentDto dstChildren = await GetChildrenAsync(dst.Id);
            Assert.That(dstChildren.Nodes.Any(n => n.Id == moving.Id), Is.True);
        }

        [Test]
        public async Task MoveNode_SameParent_IsNoOp()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "folder");
            NodeDto child = await CreateFolderAsync(folder.Id, "child");

            HttpResponseMessage res = await MoveNodeAsync(child.Id, folder.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task MoveNode_RootNode_Returns403()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");

            HttpResponseMessage res = await MoveNodeAsync(root.Id, dst.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task MoveNode_IntoSelf_Returns400()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "folder");

            HttpResponseMessage res = await MoveNodeAsync(folder.Id, folder.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task MoveNode_IntoDescendant_Returns400()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto parent = await CreateFolderAsync(root.Id, "parent");
            NodeDto middle = await CreateFolderAsync(parent.Id, "middle");
            NodeDto leaf = await CreateFolderAsync(middle.Id, "leaf");

            HttpResponseMessage res = await MoveNodeAsync(parent.Id, leaf.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task MoveNode_IntoDeepUnrelatedFolder_Succeeds()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto moving = await CreateFolderAsync(root.Id, "moving");

            Guid deepestId;
            using (IServiceScope scope = _factory!.Services.CreateScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                Node rootEntity = await db.Nodes.AsNoTracking().SingleAsync(n => n.Id == root.Id);
                Guid parentId = root.Id;

                for (int i = 0; i < 300; i++)
                {
                    Node node = new Cotton.Database.Models.Node
                    {
                        LayoutId = rootEntity.LayoutId,
                        OwnerId = rootEntity.OwnerId,
                        Type = Cotton.Database.Models.Enums.NodeType.Default,
                        ParentId = parentId,
                    };
                    node.SetName($"deep-{i:D3}");
                    db.Nodes.Add(node);
                    await db.SaveChangesAsync();
                    parentId = node.Id;
                }

                deepestId = parentId;
            }

            HttpResponseMessage res = await MoveNodeAsync(moving.Id, deepestId);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using IServiceScope verifyScope = _factory!.Services.CreateScope();
            CottonDbContext verifyDb = verifyScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Node moved = await verifyDb.Nodes.AsNoTracking().SingleAsync(n => n.Id == moving.Id);
            Assert.That(moved.ParentId, Is.EqualTo(deepestId));
        }

        [Test]
        public async Task MoveNode_NameCollisionWithSiblingFolder_Returns409()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src = await CreateFolderAsync(root.Id, "src");
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");
            NodeDto moving = await CreateFolderAsync(src.Id, "thing");
            await CreateFolderAsync(dst.Id, "thing");

            HttpResponseMessage res = await MoveNodeAsync(moving.Id, dst.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public async Task MoveNode_NameCollisionWithSiblingFile_Returns409()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src = await CreateFolderAsync(root.Id, "src");
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");
            NodeDto moving = await CreateFolderAsync(src.Id, "thing");
            await CreateFileAsync(dst.Id, "thing", "blocker");

            HttpResponseMessage res = await MoveNodeAsync(moving.Id, dst.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public async Task MoveNode_TargetNotFound_Returns404()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "folder");

            HttpResponseMessage res = await MoveNodeAsync(folder.Id, Guid.NewGuid());
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

    }
}
