// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [Test]
        public async Task PreviewQueue_UsesEachRegisteredGeneratorsOwnVersion()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            List<Guid> staleIds = [];
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            foreach (KeyValuePair<string, int> generator in PreviewGeneratorProvider.GetGeneratorVersions())
            {
                NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, generator.Key + ".txt", "text/plain",
                    Encoding.UTF8.GetBytes(generator.Key));
                FileManifest manifest = await LoadFileManifestAsync(dbContext, file.Id);
                manifest.SmallFilePreviewHash = [1];
                manifest.SmallFilePreviewHashEncrypted = [2];
                manifest.PreviewGeneratorId = generator.Key;
                manifest.PreviewGeneratorVersion = generator.Value - 1;
                staleIds.Add(manifest.Id);
            }
            await dbContext.SaveChangesAsync();
            Assert.That(await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None),
                Is.EquivalentTo(staleIds));

            foreach (FileManifest manifest in await dbContext.FileManifests.Where(manifest => staleIds.Contains(manifest.Id)).ToListAsync())
            {
                manifest.PreviewGeneratorVersion++;
            }
            await dbContext.SaveChangesAsync();
            Assert.That(await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None), Is.Empty);
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(null)]
        public async Task PreviewPipeline_ExistingReadyPreviewWithoutGenerator_IsPreserved(int? previousVersion)
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, "photo.png", "image/png", CreateGradientPngBytes(96, 64));
            await ExecuteGeneratePreviewJobAsync();
            int savedVersion = previousVersion ?? PreviewGeneratorProvider.FailedAttemptVersion;
            await UpdateFileManifestAsync(file.Id, manifest =>
            {
                manifest.PreviewGeneratorId = null;
                manifest.PreviewGeneratorVersion = savedVersion;
            });
            FileManifestPreviewState before = await GetFileManifestByNodeFileIdAsync(file.Id);

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CallbackStoragePipeline storage = new(scope.ServiceProvider.GetRequiredService<IStoragePipeline>(), _ => Task.CompletedTask);
            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);
            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);

            FileManifestPreviewState after = await GetFileManifestByNodeFileIdAsync(file.Id);
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await LoadFileManifestAsync(dbContext, file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(storage.ReadCount, Is.Zero);
                Assert.That(storage.WriteCount, Is.Zero);
                Assert.That(after.SmallFilePreviewHash, Is.EqualTo(before.SmallFilePreviewHash));
                Assert.That(after.SmallFilePreviewHashEncrypted, Is.EqualTo(before.SmallFilePreviewHashEncrypted));
                Assert.That(after.LargeFilePreviewHash, Is.EqualTo(before.LargeFilePreviewHash));
                Assert.That(manifest.PreviewGeneratorId, Is.Null);
                Assert.That(manifest.PreviewGeneratorVersion, Is.EqualTo(savedVersion));
            });
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                .RequireValid(dbContext, manifest, "test.preview-existing");
        }

        [Test]
        public async Task PreviewQueue_StaleImageVersion_DoesNotRequeueOtherGenerators()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto image = await UploadAndCreateFileAsync(root.Id, "photo.png", "image/png", CreateGradientPngBytes(96, 64));
            NodeFileManifestDto text = await UploadAndCreateFileAsync(root.Id, "note.txt", "text/plain", "Preserved text"u8.ToArray());
            await ExecuteGeneratePreviewJobAsync();
            await UpdateFileManifestAsync(image.Id, manifest => manifest.PreviewGeneratorVersion--);

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            List<Guid> pending = await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None);
            Assert.That(pending, Is.EqualTo(new[] { image.FileManifestId }));
            FileManifest textManifest = await LoadFileManifestAsync(dbContext, text.Id);
            Assert.That(textManifest.PreviewGeneratorId, Is.EqualTo(new TextPreviewGenerator().Id));
            Assert.That(textManifest.PreviewGeneratorVersion, Is.EqualTo(new TextPreviewGenerator().Version));

            await ExecuteGeneratePreviewJobAsync();
            Assert.That(await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None), Is.Empty);
        }

        [Test]
        public async Task PreviewPipeline_MissingPreviewWithoutGenerator_IsGeneratedAndIdentified()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, "note.txt", "text/plain", "Missing preview"u8.ToArray());
            await UpdateFileManifestAsync(file.Id, manifest => manifest.PreviewGeneratorVersion = PreviewGeneratorProvider.FailedAttemptVersion);

            await ExecuteGeneratePreviewJobAsync();

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await LoadFileManifestAsync(dbContext, file.Id);
            Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
            Assert.That(manifest.PreviewGeneratorId, Is.EqualTo(new TextPreviewGenerator().Id));
            Assert.That(manifest.PreviewGeneratorVersion, Is.EqualTo(new TextPreviewGenerator().Version));
        }

        [Test]
        public async Task PreviewPipeline_FailedRefresh_PreservesCachedPreviewAndRetriesOnlyAfterGeneratorChanges()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, "note.txt", "text/plain", "Not an image"u8.ToArray());
            await ExecuteGeneratePreviewJobAsync();
            FileManifestPreviewState before = await GetFileManifestByNodeFileIdAsync(file.Id);
            await RenamePreviewFileAsync(file.Id, "note.png");
            await UpdateFileManifestAsync(file.Id, manifest => manifest.PreviewGeneratorVersion--);

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CallbackStoragePipeline storage = new(scope.ServiceProvider.GetRequiredService<IStoragePipeline>(), _ => Task.CompletedTask);
            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);
            int readsAfterFailure = storage.ReadCount;
            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);
            FileManifestPreviewState failed = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(readsAfterFailure, Is.Positive);
                Assert.That(storage.ReadCount, Is.EqualTo(readsAfterFailure));
                Assert.That(storage.WriteCount, Is.Zero);
                Assert.That(failed.SmallFilePreviewHash, Is.EqualTo(before.SmallFilePreviewHash));
                Assert.That(failed.SmallFilePreviewHashEncrypted, Is.EqualTo(before.SmallFilePreviewHashEncrypted));
                Assert.That(failed.PreviewGenerationError, Is.Not.Null);
            });

            await UpdateFileManifestAsync(file.Id, manifest => manifest.PreviewGeneratorVersion ^= 1);
            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);
            Assert.That(storage.ReadCount, Is.GreaterThan(readsAfterFailure));
            int readsAfterRetry = storage.ReadCount;
            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);
            Assert.That(storage.ReadCount, Is.EqualTo(readsAfterRetry));
        }
    }
}
