// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task IntegrityVersionUpgrade_SavesLatestSignatures_AndRejectsConcurrentLegacyWrite()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeFileManifestDto created = await UploadTextFileAsync(root, "legacy.txt", "Legacy signed content");
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile file = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file, 1, scope.ServiceProvider);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file.FileManifest, 1, scope.ServiceProvider);
            dbContext.ChangeTracker.Clear();

            file = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            IDatabaseIntegrityVerifier verifier = scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>();
            verifier.RequireValid(dbContext, file, "test.legacy-file");
            verifier.RequireValid(dbContext, file.FileManifest, "test.legacy-manifest");
            await using AsyncServiceScope staleScope = _factory.Services.CreateAsyncScope();
            CottonDbContext staleContext = staleScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile stale = await staleContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);

            file.SetName("upgraded.md");
            file.FileManifest.Metadata = new Dictionary<string, string> { ["label"] = "Updated" };
            await dbContext.SaveChangesAsync();
            stale.SetName("stale.pdf");
            stale.FileManifest.Metadata = new Dictionary<string, string> { ["label"] = "Stale" };
            Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());

            dbContext.ChangeTracker.Clear();
            NodeFile stored = await dbContext.NodeFiles.Include(entity => entity.FileManifest).SingleAsync(entity => entity.Id == created.Id);
            Assert.Multiple(() =>
            {
                Assert.That(stored.Name, Is.EqualTo("upgraded.md"));
                Assert.That(stored.ContentType, Is.EqualTo("text/markdown"));
                Assert.That(stored.FileManifest.Metadata?["label"], Is.EqualTo("Updated"));
                Assert.That(dbContext.Entry(stored).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                    Is.EqualTo(NodeFileIntegrityDescriptor.LatestVersion));
                Assert.That(dbContext.Entry(stored.FileManifest).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                    Is.EqualTo(FileManifestIntegrityDescriptor.LatestVersion));
            });
            verifier.RequireValid(dbContext, stored, "test.upgraded-file");
            verifier.RequireValid(dbContext, stored.FileManifest, "test.upgraded-manifest");
        }

        [TestCase(false, 2, false)]
        [TestCase(true, 2, false)]
        [TestCase(false, 1, false)]
        [TestCase(true, 1, false)]
        [TestCase(false, 1, true)]
        [TestCase(true, 1, true)]
        public async Task ContentTypeMigration_BackfillChecksSignatureBeforeUpdating(bool tampered, int version, bool populated)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeFileManifestDto created = await UploadTextFileAsync(root, "empty-type.txt", "Empty content type");
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile file = await dbContext.NodeFiles.SingleAsync(entity => entity.Id == created.Id);
            string initialType = populated ? "application/custom" : string.Empty;
            dbContext.Entry(file).Property(entity => entity.ContentType).CurrentValue = initialType;
            await dbContext.SaveChangesAsync();
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file, version, scope.ServiceProvider);
            dbContext.ChangeTracker.Clear();
            if (tampered)
            {
                await dbContext.NodeFiles.Where(entity => entity.Id == created.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.Name, "tampered.txt"));
            }
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            if (tampered)
            {
                Assert.ThrowsAsync<DatabaseIntegrityException>(() =>
                    mediator.Send(new BackfillNodeFileContentTypesRequest(), CancellationToken.None));
                Assert.That(await dbContext.NodeFiles.Where(entity => entity.Id == created.Id)
                    .Select(entity => entity.ContentType).SingleAsync(), Is.EqualTo(initialType));
                Assert.That(await dbContext.NodeFiles.Where(entity => entity.Id == created.Id)
                    .Select(entity => EF.Property<int?>(entity, DatabaseIntegrityColumns.VersionProperty)).SingleAsync(),
                    Is.EqualTo(version));
                return;
            }

            Assert.That(await mediator.Send(new BackfillNodeFileContentTypesRequest(), CancellationToken.None), Is.EqualTo(1));
            NodeFile stored = await dbContext.NodeFiles.SingleAsync(entity => entity.Id == created.Id);
            Assert.That(stored.ContentType, Is.EqualTo(populated ? initialType : "text/plain"));
            Assert.That(dbContext.Entry(stored).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                Is.EqualTo(NodeFileIntegrityDescriptor.LatestVersion));
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                .RequireValid(dbContext, stored, "test.backfilled-current-signature");
        }

    }
}
