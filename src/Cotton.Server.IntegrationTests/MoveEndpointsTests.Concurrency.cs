// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class MoveEndpointsTests
    {
        [Test]
        public async Task ConcurrentMoveFileAndCreateFolder_SameNameSameTarget_OnlyOneWins()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src = await CreateFolderAsync(root.Id, "src");
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");
            NodeFileManifestDto movingFile = await CreateFileAsync(src.Id, "thing", "cross-handler-race");

            // Without the lock applied to CreateNode too, MoveFile would see no folder
            // "thing" in dst and CreateNode would see no file "thing" in dst — both
            // pre-checks pass independently and dst would end up with both.
            Task<HttpResponseMessage> moveFile = MoveFileAsync(movingFile.Id, dst.Id);
            Task<HttpResponseMessage> createFolder = _client!.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = dst.Id, Name = "thing" });
            HttpResponseMessage[] results = await Task.WhenAll(moveFile, createFolder);

            int oks = results.Count(r => r.StatusCode == HttpStatusCode.OK);
            int conflicts = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.That(oks, Is.EqualTo(1), "Exactly one cross-handler write must win.");
            Assert.That(conflicts, Is.EqualTo(1), "The other must be rejected as duplicate.");

            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            int fileInDst = await db.NodeFiles.AsNoTracking().CountAsync(f => f.NodeId == dst.Id && f.NameKey == "thing");
            int folderInDst = await db.Nodes.AsNoTracking().CountAsync(n => n.ParentId == dst.Id && n.NameKey == "thing");
            Assert.That(fileInDst + folderInDst, Is.EqualTo(1),
                "Destination must have exactly one entry named 'thing' across both tables.");
        }

        [Test]
        public async Task ConcurrentCreateFileAndCreateFolder_SameNameSameTarget_OnlyOneWins()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto target = await CreateFolderAsync(root.Id, "dst");
            string hash = await UploadChunkViaClientAsync(_client!, "create-file-folder-race");

            Task<HttpResponseMessage> createFile = _client!.PostAsJsonAsync(
                "/api/v1/files/from-chunks",
                new CreateFileFromChunksRequestDto
                {
                    ChunkHashes = [hash],
                    Name = "thing",
                    ContentType = "application/octet-stream",
                    Hash = hash,
                    NodeId = target.Id
                });
            Task<HttpResponseMessage> createFolder = _client!.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = target.Id, Name = "thing" });

            HttpResponseMessage[] results = await Task.WhenAll(createFile, createFolder);

            int oks = results.Count(r => r.StatusCode == HttpStatusCode.OK);
            int conflicts = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.That(oks, Is.EqualTo(1), "Exactly one cross-table create must win.");
            Assert.That(conflicts, Is.EqualTo(1), "The other must be rejected as duplicate.");

            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            int fileInDst = await db.NodeFiles.AsNoTracking().CountAsync(f => f.NodeId == target.Id && f.NameKey == "thing");
            int folderInDst = await db.Nodes.AsNoTracking().CountAsync(n => n.ParentId == target.Id && n.NameKey == "thing");
            Assert.That(fileInDst + folderInDst, Is.EqualTo(1),
                "Destination must have exactly one entry named 'thing' across both tables.");
        }

        [Test]
        public async Task ConcurrentMoveFileAndMoveNode_SameNameSameTarget_OnlyOneWins()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src1 = await CreateFolderAsync(root.Id, "src1");
            NodeDto src2 = await CreateFolderAsync(root.Id, "src2");
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");
            NodeFileManifestDto movingFile = await CreateFileAsync(src1.Id, "thing", "cross-table-race");
            NodeDto movingFolder = await CreateFolderAsync(src2.Id, "thing");

            // Without the per-layout advisory lock, file's collision pre-check and
            // folder's collision pre-check would both pass on the pre-update tree
            // and both commits would land — dst would end up with both a file and a
            // folder named "thing", which the create/rename paths normally forbid.
            Task<HttpResponseMessage> moveFile = MoveFileAsync(movingFile.Id, dst.Id);
            Task<HttpResponseMessage> moveFolder = MoveNodeAsync(movingFolder.Id, dst.Id);
            HttpResponseMessage[] results = await Task.WhenAll(moveFile, moveFolder);

            int oks = results.Count(r => r.StatusCode == HttpStatusCode.OK);
            int conflicts = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.That(oks, Is.EqualTo(1), "Exactly one cross-table move must win.");
            Assert.That(conflicts, Is.EqualTo(1), "The other must be rejected as duplicate.");

            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            int fileInDst = await db.NodeFiles.AsNoTracking().CountAsync(f => f.NodeId == dst.Id && f.NameKey == "thing");
            int folderInDst = await db.Nodes.AsNoTracking().CountAsync(n => n.ParentId == dst.Id && n.NameKey == "thing");
            Assert.That(fileInDst + folderInDst, Is.EqualTo(1),
                "Destination must have exactly one entry named 'thing' across both tables.");
        }

        [Test]
        public async Task MoveNode_ConcurrentSwap_DoesNotCreateCycle()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto a = await CreateFolderAsync(root.Id, "a");
            NodeDto b = await CreateFolderAsync(root.Id, "b");

            // Without the per-layout advisory lock, both descendant checks could pass
            // on the pre-update tree and both commits would land — leaving A.parent=B
            // and B.parent=A. With the lock the second request re-runs the descendant
            // check inside the lock and rejects as into-descendant.
            Task<HttpResponseMessage> moveAIntoB = MoveNodeAsync(a.Id, b.Id);
            Task<HttpResponseMessage> moveBIntoA = MoveNodeAsync(b.Id, a.Id);
            HttpResponseMessage[] results = await Task.WhenAll(moveAIntoB, moveBIntoA);

            int oks = results.Count(r => r.StatusCode == HttpStatusCode.OK);
            int bads = results.Count(r => r.StatusCode == HttpStatusCode.BadRequest);
            Assert.That(oks, Is.EqualTo(1), "Exactly one swap leg must succeed.");
            Assert.That(bads, Is.EqualTo(1), "The losing leg must be rejected (into-descendant).");

            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(await ParentWalkReachesRoot(db, a.Id), Is.True, "A must reach the root with no cycle.");
            Assert.That(await ParentWalkReachesRoot(db, b.Id), Is.True, "B must reach the root with no cycle.");
        }

        [Test]
        public async Task MoveNode_NonDefaultType_Returns404()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");

            // Build a Trash-type sibling under root via the DI scope — the API does
            // not expose creation of non-Default nodes.
            Guid trashNodeId;
            using (IServiceScope scope = _factory!.Services.CreateScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                Guid ownerId = await db.Users.AsNoTracking().Select(u => u.Id).FirstAsync();
                Node rootEntity = await db.Nodes.AsNoTracking().SingleAsync(n => n.Id == root.Id);
                Node trash = new Cotton.Database.Models.Node
                {
                    LayoutId = rootEntity.LayoutId,
                    OwnerId = ownerId,
                    Type = Cotton.Database.Models.Enums.NodeType.Trash,
                    ParentId = rootEntity.Id,
                };
                trash.SetName("trash-thing");
                db.Nodes.Add(trash);
                await db.SaveChangesAsync();
                trashNodeId = trash.Id;
            }

            HttpResponseMessage res = await MoveNodeAsync(trashNodeId, dst.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "Move endpoint must reject non-Default node types as not-found (no leak).");
        }

        [Test]
        public async Task MoveNode_AcrossLayouts_Returns400()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto moving = await CreateFolderAsync(root.Id, "moving");

            (Guid OwnerId, Guid RootId) additionalLayout = await CreateAdditionalLayoutRootAsync(
                _factory!.Services,
                "other-root");

            HttpResponseMessage res = await MoveNodeAsync(moving.Id, additionalLayout.RootId);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        // ---------------------------------------------------------------------
        // Notification failure does not fail the move
        // ---------------------------------------------------------------------

        [Test]
        public async Task ConcurrentWebDavPutAndMkCol_SameNameSameTarget_OnlyOneWins()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto target = await CreateFolderAsync(root.Id, "webdav-race");

            await UseWebDavBasicAuthAsync(_client!);

            Task<HttpResponseMessage> putFile = SendWebDavPutAsync(_client!, "/api/v1/webdav/webdav-race/thing", "webdav-put-race");
            Task<HttpResponseMessage> mkcol = SendWebDavMkColAsync(_client!, "/api/v1/webdav/webdav-race/thing");
            HttpResponseMessage[] results = await Task.WhenAll(putFile, mkcol);

            int successes = results.Count(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.NoContent);
            int rejections = results.Count(r => r.StatusCode is HttpStatusCode.Conflict
                or HttpStatusCode.MethodNotAllowed
                or HttpStatusCode.PreconditionFailed);
            Assert.That(successes, Is.EqualTo(1), "Exactly one WebDAV namespace write must win.");
            Assert.That(rejections, Is.EqualTo(1), "The other WebDAV write must be rejected.");

            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            int fileInDst = await db.NodeFiles.AsNoTracking().CountAsync(f => f.NodeId == target.Id && f.NameKey == "thing");
            int folderInDst = await db.Nodes.AsNoTracking().CountAsync(n => n.ParentId == target.Id && n.NameKey == "thing");
            Assert.That(fileInDst + folderInDst, Is.EqualTo(1),
                "Destination must have exactly one WebDAV entry named 'thing' across both tables.");
        }

        [Test]
        public async Task ConcurrentFileUpdates_AcrossLayouts_DoNotExceedUserStorageQuota()
        {
            _client?.Dispose();
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
                _factory = null;
            }

            QuotaMutationBarrier barrier = new();
            using TestAppFactory factory = new(_overrides);
            using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ILayoutMutationGate>();
                    services.AddSingleton(barrier);
                    services.AddSingleton<ILayoutMutationGate>(serviceProvider =>
                        new QuotaBarrierLayoutMutationGate(
                            serviceProvider.GetRequiredService<QuotaMutationBarrier>()));
                });
            });
            using HttpClient client = customFactory.CreateClient(
                new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            string token = await LoginViaClientAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage quotaResponse = await client.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-storage-quota-bytes",
                10L);
            quotaResponse.EnsureSuccessStatusCode();
            try
            {
                NodeDto? primaryRoot = await client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
                Assert.That(primaryRoot, Is.Not.Null);
                (Guid OwnerId, Guid RootId) additionalLayout = await CreateAdditionalLayoutRootAsync(
                    customFactory.Services,
                    "quota-other-root");
                Guid primaryFileId = await CreateEmptyFileAsync(
                    customFactory.Services,
                    additionalLayout.OwnerId,
                    primaryRoot!.Id,
                    "quota-a.txt");
                Guid secondaryFileId = await CreateEmptyFileAsync(
                    customFactory.Services,
                    additionalLayout.OwnerId,
                    additionalLayout.RootId,
                    "quota-b.txt");
                string firstHash = await UploadChunkViaClientAsync(client, "123456");
                string secondHash = await UploadChunkViaClientAsync(client, "abcdef");
                barrier.Enable();

                Task<HttpResponseMessage> firstUpdate = SendUpdateFileViaClientAsync(
                    client,
                    primaryFileId,
                    primaryRoot.Id,
                    "quota-a.txt",
                    firstHash);
                Task<HttpResponseMessage> secondUpdate = SendUpdateFileViaClientAsync(
                    client,
                    secondaryFileId,
                    additionalLayout.RootId,
                    "quota-b.txt",
                    secondHash);
                HttpResponseMessage[] responses = await Task.WhenAll(firstUpdate, secondUpdate);
                using HttpResponseMessage firstResponse = responses[0];
                using HttpResponseMessage secondResponse = responses[1];

                int successes = responses.Count(response => response.IsSuccessStatusCode);
                int quotaRejections = responses.Count(response => response.StatusCode == (HttpStatusCode)507);
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(successes, Is.EqualTo(1));
                    Assert.That(quotaRejections, Is.EqualTo(1));
                }

                using IServiceScope scope = customFactory.Services.CreateScope();
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                long usedBytes = await dbContext.NodeFiles
                    .AsNoTracking()
                    .Where(nodeFile => nodeFile.OwnerId == additionalLayout.OwnerId)
                    .SumAsync(nodeFile => nodeFile.FileManifest.SizeBytes);
                Assert.That(usedBytes, Is.EqualTo(6));
            }
            finally
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using HttpResponseMessage resetQuotaResponse = await client.PatchAsJsonAsync<long?>(
                    "/api/v1/server/settings/default-user-storage-quota-bytes",
                    null);
                resetQuotaResponse.EnsureSuccessStatusCode();
            }
        }

    }
}
