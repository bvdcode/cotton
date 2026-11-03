// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public class BulkNodeMetadataTests : MoveEndpointTestBase
    {
        [Test]
        public async Task BulkPatch_UpdatesEntireSubtreeAndPreservesOtherFieldsAndSignatures()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto target = await CreateFolderAsync(root.Id, "target");
            NodeDto nested = await CreateFolderAsync(target.Id, "nested");
            NodeDto sibling = await CreateFolderAsync(root.Id, "sibling");
            using (IServiceScope scope = _factory!.Services.CreateScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                Node parent = await db.Nodes.SingleAsync(node => node.Id == nested.Id);
                parent.Metadata = new() { ["description"] = "preserved", ["category"] = "old" };
                for (int index = 0; index < 300; index++)
                {
                    db.Nodes.Add(parent.CopyTo(parent, $"child-{index}"));
                }
                await db.SaveChangesAsync();
            }

            using HttpResponseMessage response = await BulkPatchAsync(target.Id,
                new Dictionary<string, string> { ["category"] = "archive" }, true);
            response.EnsureSuccessStatusCode();
            List<NodeDto> updated = (await response.Content.ReadFromJsonAsync<List<NodeDto>>())!;
            Assert.That(updated, Has.Count.EqualTo(302));
            using IServiceScope readScope = _factory!.Services.CreateScope();
            CottonDbContext readDb = readScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            IDatabaseIntegrityVerifier verifier = readScope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>();
            Guid[] ids = [.. updated.Select(node => node.Id)];
            List<Node> nodes = await readDb.Nodes.Where(node => ids.Contains(node.Id)).ToListAsync();
            foreach (Node node in nodes)
            {
                Assert.That(node.Metadata!["category"], Is.EqualTo("archive"));
                if (node.Id != target.Id)
                {
                    Assert.That(node.Metadata["description"], Is.EqualTo("preserved"));
                }
                verifier.RequireValid(readDb, node, "test.bulk-metadata");
            }
            Node untouched = await readDb.Nodes.SingleAsync(node => node.Id == sibling.Id);
            Assert.That(untouched.Metadata?.ContainsKey("category") ?? false, Is.False);
            verifier.RequireValid(readDb, untouched, "test.bulk-metadata-sibling");
        }

        [Test]
        public async Task NonrecursivePatch_OnlyUpdatesRootAndSingleEndpointRetainsItsContract()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto child = await CreateFolderAsync(root.Id, "child");
            using HttpResponseMessage bulk = await BulkPatchAsync(root.Id,
                new Dictionary<string, string> { ["category"] = "root" });
            bulk.EnsureSuccessStatusCode();
            List<NodeDto> nodes = (await bulk.Content.ReadFromJsonAsync<List<NodeDto>>())!;
            Assert.That(nodes.Select(node => node.Id), Is.EqualTo(new[] { root.Id }));
            using HttpResponseMessage single = await _client!.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{root.Id}/metadata", new Dictionary<string, string> { ["category"] = "updated" });
            single.EnsureSuccessStatusCode();
            NodeDto updated = (await single.Content.ReadFromJsonAsync<NodeDto>())!;
            Assert.That(updated.Metadata!["category"], Is.EqualTo("updated"));
            NodeDto untouched = (await _client.GetFromJsonAsync<NodeDto>($"/api/v1/layouts/nodes/{child.Id}"))!;
            Assert.That(untouched.Metadata?.ContainsKey("category") ?? false, Is.False);
        }

        [TestCase("{\"category\":null}")]
        [TestCase("{\" \":\"value\"}")]
        [TestCase("{\"category\":123}")]
        [TestCase("null")]
        public async Task BulkPatch_RejectsInvalidFieldsWithoutChangingMetadata(string json)
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            using StringContent content = new(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _client!.PatchAsync(
                $"/api/v1/layouts/nodes/{root.Id}/metadata/bulk?includeDescendants=true", content);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            NodeDto unchanged = (await _client.GetFromJsonAsync<NodeDto>($"/api/v1/layouts/nodes/{root.Id}"))!;
            Assert.That(unchanged.Metadata, Is.EquivalentTo(root.Metadata));
        }

        [Test]
        public async Task BulkPatch_RejectsMissingAndForeignRoots()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            using HttpResponseMessage missing = await BulkPatchAsync(Guid.NewGuid(),
                new Dictionary<string, string> { ["category"] = "archive" }, true);
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            using HttpResponseMessage created = await _client!.PostAsJsonAsync("/api/v1/users",
                new Cotton.Server.Handlers.Users.AdminCreateUserRequest("other-user", "other@example.com",
                    "testpassword", UserRole.User));
            created.EnsureSuccessStatusCode();
            using HttpResponseMessage login = await _client!.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequestDto { Username = "other-user", Password = "testpassword" });
            login.EnsureSuccessStatusCode();
            TokenPairResponseDto tokens = (await login.Content.ReadFromJsonAsync<TokenPairResponseDto>())!;
            _client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
            using HttpResponseMessage foreign = await BulkPatchAsync(root.Id,
                new Dictionary<string, string> { ["category"] = "archive" }, true);
            Assert.That(foreign.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            using IServiceScope scope = _factory!.Services.CreateScope();
            Node untouched = await scope.ServiceProvider.GetRequiredService<CottonDbContext>()
                .Nodes.SingleAsync(node => node.Id == root.Id);
            Assert.That(untouched.Metadata?.ContainsKey("category") ?? false, Is.False);
        }

        [Test]
        public async Task BulkPatch_RollsBackEarlierBatchesWhenLaterSaveFails()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto child = await CreateFolderAsync(root.Id, "child");
            RejectSecondMetadataBatchInterceptor interceptor = new();
            using TestAppFactory failing = new(_overrides, services => services.AddDbContext<CottonDbContext>(
                (_, options) => options.AddInterceptors(interceptor)));
            using HttpClient client = failing.CreateClient();
            client.DefaultRequestHeaders.Authorization = _client!.DefaultRequestHeaders.Authorization;
            using HttpResponseMessage response = await client.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{root.Id}/metadata/bulk?includeDescendants=true",
                new Dictionary<string, string> { ["category"] = "archive" });
            Assert.That(response.IsSuccessStatusCode, Is.False);
            Assert.That(interceptor.SaveAttempts, Is.EqualTo(2));

            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            List<Node> nodes = await db.Nodes.Where(node => node.Id == root.Id || node.Id == child.Id).ToListAsync();
            foreach (Node node in nodes)
            {
                Assert.That(node.Metadata?.ContainsKey("category") ?? false, Is.False);
                scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                    .RequireValid(db, node, "test.bulk-metadata-rollback");
            }
        }

        private Task<HttpResponseMessage> BulkPatchAsync(Guid rootId,
            Dictionary<string, string> patch, bool includeDescendants = false)
        {
            return _client!.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{rootId}/metadata/bulk?includeDescendants={includeDescendants}", patch);
        }
    }
}
