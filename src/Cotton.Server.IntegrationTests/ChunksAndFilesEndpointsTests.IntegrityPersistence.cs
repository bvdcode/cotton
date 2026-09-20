// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [TestCase(1)]
        [TestCase(2)]
        public async Task IntegrityPersistence_FailedSaveRollsBackSignatures_AndRetryUsesSameContext(int initialVersion)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeFileManifestDto created = await UploadTextFileAsync(root, "retry.txt", "Retry signed content");
            NodeFileManifestDto other = await UploadTextFileAsync(root, "other.txt", "Conflicting manifest");
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile file = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file, initialVersion, scope.ServiceProvider);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file.FileManifest, initialVersion, scope.ServiceProvider);
            dbContext.ChangeTracker.Clear();
            file = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            byte[] originalHash = file.FileManifest.ProposedContentHash.ToArray();
            byte[] originalFileMac = dbContext.Entry(file).Property<byte[]?>(DatabaseIntegrityColumns.MacProperty).CurrentValue!.ToArray();
            byte[] originalManifestMac = dbContext.Entry(file.FileManifest).Property<byte[]?>(DatabaseIntegrityColumns.MacProperty).CurrentValue!.ToArray();
            file.SetName("retried.md");
            file.FileManifest.ProposedContentHash = await dbContext.FileManifests.Where(entity => entity.Id == other.FileManifestId)
                .Select(entity => entity.ProposedContentHash).SingleAsync();

            DbUpdateException? failure = Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            Assert.That(failure!.InnerException, Is.InstanceOf<PostgresException>());
            Assert.That(((PostgresException)failure.InnerException!).SqlState, Is.EqualTo(PostgresErrorCodes.UniqueViolation));
            await using (AsyncServiceScope verificationScope = _factory.Services.CreateAsyncScope())
            {
                CottonDbContext verificationContext = verificationScope.ServiceProvider.GetRequiredService<CottonDbContext>();
                NodeFile persisted = await verificationContext.NodeFiles.Include(entity => entity.FileManifest)
                    .SingleAsync(entity => entity.Id == created.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(persisted.Name, Is.EqualTo("retry.txt"));
                    Assert.That(persisted.FileManifest.ProposedContentHash, Is.EqualTo(originalHash));
                    Assert.That(verificationContext.Entry(persisted).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                        Is.EqualTo(initialVersion));
                    Assert.That(verificationContext.Entry(persisted.FileManifest).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                        Is.EqualTo(initialVersion));
                    Assert.That(verificationContext.Entry(persisted).Property<byte[]?>(DatabaseIntegrityColumns.MacProperty).CurrentValue,
                        Is.EqualTo(originalFileMac));
                    Assert.That(verificationContext.Entry(persisted.FileManifest).Property<byte[]?>(DatabaseIntegrityColumns.MacProperty).CurrentValue,
                        Is.EqualTo(originalManifestMac));
                });
                VerifyFileAndManifest(verificationContext, persisted, verificationScope.ServiceProvider);
            }

            file.FileManifest.ProposedContentHash = originalHash;
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
            NodeFile retried = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            Assert.That(retried.Name, Is.EqualTo("retried.md"));
            Assert.That(retried.ContentType, Is.EqualTo("text/markdown"));
            VerifyFileAndManifest(dbContext, retried, scope.ServiceProvider);
        }

        [TestCase(1)]
        [TestCase(2)]
        public async Task IntegrityPersistence_RepeatedSavesAndNoOpSaves_PreserveValidSignatures(int initialVersion)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeFileManifestDto created = await UploadTextFileAsync(root, "repeat.txt", "Repeated signed content");
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile file = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file, initialVersion, scope.ServiceProvider);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file.FileManifest, initialVersion, scope.ServiceProvider);
            dbContext.ChangeTracker.Clear();
            file = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            Assert.That(await dbContext.SaveChangesAsync(), Is.Zero);
            Assert.That(dbContext.Entry(file).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue, Is.EqualTo(initialVersion));

            foreach (string name in new[] { "документ.md", "document.PDF", "audio.ogg", "source.ts", "repeat.txt" })
            {
                file.SetName(name);
                file.Metadata = new Dictionary<string, string> { ["z"] = name, ["a"] = "First" };
                file.FileManifest.Metadata = new Dictionary<string, string> { ["label"] = name };
                await dbContext.SaveChangesAsync();
                VerifyFileAndManifest(dbContext, file, scope.ServiceProvider);
                byte[] mac = dbContext.Entry(file).Property<byte[]?>(DatabaseIntegrityColumns.MacProperty).CurrentValue!.ToArray();
                Assert.That(await dbContext.SaveChangesAsync(), Is.Zero);
                Assert.That(dbContext.Entry(file).Property<byte[]?>(DatabaseIntegrityColumns.MacProperty).CurrentValue, Is.EqualTo(mac));
                await using AsyncServiceScope verificationScope = _factory.Services.CreateAsyncScope();
                CottonDbContext verificationContext = verificationScope.ServiceProvider.GetRequiredService<CottonDbContext>();
                NodeFile persisted = await verificationContext.NodeFiles.Include(entity => entity.FileManifest)
                    .SingleAsync(entity => entity.Id == created.Id);
                Assert.That(persisted.Name, Is.EqualTo(name));
                VerifyFileAndManifest(verificationContext, persisted, verificationScope.ServiceProvider);
            }
        }

        [TestCase(1, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 1)]
        [TestCase(2, 2)]
        public async Task IntegrityPersistence_MixedVersions_ApiAndWebDavReadWithoutResigning(int fileVersion, int manifestVersion)
        {
            const string content = "Mixed signature versions";
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeFileManifestDto created = await UploadTextFileAsync(root, "mixed.md", content);
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile file = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file, fileVersion, scope.ServiceProvider);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file.FileManifest, manifestVersion, scope.ServiceProvider);
            dbContext.ChangeTracker.Clear();

            using HttpResponseMessage apiResponse = await _client.GetAsync($"/api/v1/files/{created.Id}/content");
            Assert.That(apiResponse.IsSuccessStatusCode, Is.True);
            Assert.That(await apiResponse.Content.ReadAsStringAsync(), Is.EqualTo(content));
            Assert.That(apiResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/markdown"));

            string webDavToken = await GetWebDavTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{webDavToken}")));
            using HttpResponseMessage webDavResponse = await _client.GetAsync("/api/v1/webdav/mixed.md");
            Assert.That(webDavResponse.IsSuccessStatusCode, Is.True);
            Assert.That(await webDavResponse.Content.ReadAsStringAsync(), Is.EqualTo(content));
            Assert.That(webDavResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/markdown"));

            NodeFile persisted = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            VerifyFileAndManifest(dbContext, persisted, scope.ServiceProvider);
            Assert.That(dbContext.Entry(persisted).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue, Is.EqualTo(fileVersion));
            Assert.That(dbContext.Entry(persisted.FileManifest).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue, Is.EqualTo(manifestVersion));
        }

        private static void VerifyFileAndManifest(CottonDbContext dbContext, NodeFile file, IServiceProvider services)
        {
            IDatabaseIntegrityVerifier verifier = services.GetRequiredService<IDatabaseIntegrityVerifier>();
            verifier.RequireValid(dbContext, file, "test.persisted-file");
            verifier.RequireValid(dbContext, file.FileManifest, "test.persisted-manifest");
        }
    }
}
