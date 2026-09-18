// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using EasyExtensions.Mediator;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task ManifestContentTypeCleanup_UpgradesLegacyAndCurrentRowsAcrossBatches_AndIsIdempotent()
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            List<FileManifest> manifests = [];
            for (int i = 0; i < 5003; i++)
            {
                manifests.Add(new FileManifest
                {
                    ProposedContentHash = SHA256.HashData(Encoding.UTF8.GetBytes($"manifest-cleanup-{i}")),
                    ContentType = i < 5001 ? "application/incorrect" : string.Empty,
                    SizeBytes = i,
                    Metadata = new Dictionary<string, string> { ["label"] = $"Manifest {i}" },
                });
            }
            dbContext.FileManifests.AddRange(manifests);
            await dbContext.SaveChangesAsync();
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, manifests[0], 1, scope.ServiceProvider);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, manifests[5001], 1, scope.ServiceProvider);
            Guid unchangedId = manifests[5002].Id;
            DateTime unchangedUpdatedAt = await dbContext.FileManifests.Where(manifest => manifest.Id == unchangedId)
                .Select(manifest => manifest.UpdatedAt).SingleAsync();
            dbContext.ChangeTracker.Clear();

            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.That(await mediator.Send(new ClearFileManifestContentTypesRequest(), CancellationToken.None), Is.EqualTo(5002));

            List<FileManifest> updated = await dbContext.FileManifests.ToListAsync();
            IDatabaseIntegrityVerifier verifier = scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>();
            Assert.That(updated, Has.Count.EqualTo(5003));
            foreach (FileManifest manifest in updated)
            {
                Assert.That(manifest.ContentType, Is.Empty);
                Assert.That(dbContext.Entry(manifest).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                    Is.EqualTo(FileManifestIntegrityDescriptor.LatestVersion));
                Assert.That(manifest.Metadata?["label"], Is.EqualTo($"Manifest {manifest.SizeBytes}"));
                verifier.RequireValid(dbContext, manifest, "test.cleared-manifest");
            }
            Assert.That(updated.Single(manifest => manifest.Id == unchangedId).UpdatedAt, Is.EqualTo(unchangedUpdatedAt));
            Assert.That(await mediator.Send(new ClearFileManifestContentTypesRequest(), CancellationToken.None), Is.Zero);
        }

        [TestCase(1)]
        [TestCase(2)]
        public async Task ManifestContentTypeCleanup_RejectsTamperingWithoutPersistingAnyPartOfTheBatch(int version)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest valid = new()
            {
                ProposedContentHash = SHA256.HashData("Valid manifest"u8),
                ContentType = "text/plain",
                SizeBytes = 100,
            };
            FileManifest tampered = new()
            {
                ProposedContentHash = SHA256.HashData("Tampered manifest"u8),
                ContentType = "text/plain",
                SizeBytes = 200,
            };
            dbContext.FileManifests.AddRange(valid, tampered);
            await dbContext.SaveChangesAsync();
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, valid, version, scope.ServiceProvider);
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, tampered, version, scope.ServiceProvider);
            await dbContext.FileManifests.Where(manifest => manifest.Id == tampered.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(manifest => manifest.SizeBytes, 300));
            dbContext.ChangeTracker.Clear();

            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.ThrowsAsync<DatabaseIntegrityException>(() =>
                mediator.Send(new ClearFileManifestContentTypesRequest(), CancellationToken.None));

            dbContext.ChangeTracker.Clear();
            List<FileManifest> persisted = await dbContext.FileManifests.ToListAsync();
            Assert.That(persisted, Has.Count.EqualTo(2));
            foreach (FileManifest manifest in persisted)
            {
                Assert.That(manifest.ContentType, Is.EqualTo("text/plain"));
                Assert.That(dbContext.Entry(manifest).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                    Is.EqualTo(version));
            }
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                .RequireValid(dbContext, persisted.Single(manifest => manifest.Id == valid.Id), "test.unchanged-valid-manifest");
        }
    }
}
