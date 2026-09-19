// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using Cotton.Server.Services.Previews;
using Cotton.Server.Services.Search;
using EasyExtensions.Mediator;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [Test]
        public async Task PreviewPipeline_ConcurrentCleanup_RetriesCurrentManifestAndLoadsQueuedManifestFresh()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            await UploadAndCreateFileAsync(root.Id, "older.txt", "text/plain", "Older content"u8.ToArray());
            await UploadAndCreateFileAsync(root.Id, "newer.txt", "text/plain", "Newer content"u8.ToArray());
            await using (AsyncServiceScope setupScope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext setupContext = setupScope.ServiceProvider.GetRequiredService<CottonDbContext>();
                List<FileManifest> manifests = await setupContext.FileManifests.ToListAsync();
                foreach (FileManifest manifest in manifests)
                {
                    manifest.ContentType = "text/plain";
                }
                await setupContext.SaveChangesAsync();
                foreach (FileManifest manifest in manifests)
                {
                    await DatabaseIntegrityTestSignatures.SetVersionAsync(setupContext, manifest, 1, setupScope.ServiceProvider);
                }
            }

            await using AsyncServiceScope previewScope = _factory!.Services.CreateAsyncScope();
            CallbackStoragePipeline storage = new(previewScope.ServiceProvider.GetRequiredService<IStoragePipeline>(), async readCount =>
            {
                if (readCount != 1)
                {
                    return;
                }
                await using AsyncServiceScope cleanupScope = _factory.Services.CreateAsyncScope();
                IMediator mediator = cleanupScope.ServiceProvider.GetRequiredService<IMediator>();
                Assert.That(await mediator.Send(new ClearFileManifestContentTypesRequest(), CancellationToken.None), Is.EqualTo(2));
                CottonDbContext cleanupContext = cleanupScope.ServiceProvider.GetRequiredService<CottonDbContext>();
                foreach (FileManifest manifest in await cleanupContext.FileManifests.ToListAsync())
                {
                    manifest.TextIndexVersion = VectorIndexDefinition.Version;
                    manifest.Metadata = new Dictionary<string, string> { ["label"] = "Concurrent metadata" };
                }
                await cleanupContext.SaveChangesAsync();
            });
            await ExecutePreviewWithStorageAsync(previewScope.ServiceProvider, storage);

            Assert.That(storage.ReadCount, Is.EqualTo(3), "Only the in-flight preview needs a retry.");
            CottonDbContext previewContext = previewScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(previewContext.ChangeTracker.Entries(), Is.Empty);
            IDatabaseIntegrityVerifier verifier = previewScope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>();
            foreach (FileManifest manifest in await previewContext.FileManifests.ToListAsync())
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
                    Assert.That(manifest.SmallFilePreviewHashEncrypted, Is.Not.Null);
                    Assert.That(manifest.PreviewGenerationError, Is.Null);
                    Assert.That(manifest.ContentType, Is.Empty);
                    Assert.That(manifest.TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
                    Assert.That(manifest.Metadata?["label"], Is.EqualTo("Concurrent metadata"));
                    Assert.That(previewContext.Entry(manifest).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                        Is.EqualTo(FileManifestIntegrityDescriptor.LatestVersion));
                });
                verifier.RequireValid(previewContext, manifest, "test.preview-cleanup");
            }
        }

        [Test]
        public async Task PreviewPipeline_RenameWhileQueued_UsesCurrentName()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto queued = await UploadAndCreateFileAsync(root.Id, "opaque", "application/octet-stream", "Queued content"u8.ToArray());
            await UploadAndCreateFileAsync(root.Id, "first.txt", "text/plain", "First content"u8.ToArray());
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CallbackStoragePipeline storage = new(scope.ServiceProvider.GetRequiredService<IStoragePipeline>(), async readCount =>
            {
                if (readCount == 1)
                {
                    await RenamePreviewFileAsync(queued.Id, "renamed.txt");
                }
            });

            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);

            FileManifestPreviewState state = await GetFileManifestByNodeFileIdAsync(queued.Id);
            Assert.That(state.SmallFilePreviewHash, Is.Not.Null);
            Assert.That(state.PreviewGenerationError, Is.Null);
            Assert.That(storage.ReadCount, Is.EqualTo(2));
        }

        [Test]
        public async Task PreviewPipeline_UploadDuringBatch_PrioritizesNewFileAndKeepsRemainingFiles()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto older = await UploadAndCreateFileAsync(root.Id, "older.txt", "text/plain", "Older content"u8.ToArray());
            NodeFileManifestDto newer = await UploadAndCreateFileAsync(root.Id, "newer.txt", "text/plain", "Newer content"u8.ToArray());
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Guid uploadedId = Guid.Empty;
            List<Guid> renderedIds = [];
            CallbackStoragePipeline storage = new(scope.ServiceProvider.GetRequiredService<IStoragePipeline>(), async readCount =>
            {
                renderedIds.Add(dbContext.ChangeTracker.Entries<FileManifest>().Single().Entity.Id);
                if (readCount == 1)
                {
                    NodeFileManifestDto uploaded = await UploadAndCreateFileAsync(root.Id, "latest.txt", "text/plain", "Latest content"u8.ToArray());
                    uploadedId = uploaded.FileManifestId;
                }
            });

            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);

            Assert.That(renderedIds, Is.EqualTo(new[] { newer.FileManifestId, uploadedId, older.FileManifestId }));
            Assert.That(await dbContext.FileManifests.CountAsync(manifest => manifest.SmallFilePreviewHash != null), Is.EqualTo(3));
        }

        [Test]
        public async Task PreviewPipeline_CompletedWhileQueued_DoesNotRenderAgain()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto older = await UploadAndCreateFileAsync(root.Id, "older.txt", "text/plain", "Older content"u8.ToArray());
            NodeFileManifestDto newer = await UploadAndCreateFileAsync(root.Id, "newer.txt", "text/plain", "Newer content"u8.ToArray());
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CallbackStoragePipeline storage = new(scope.ServiceProvider.GetRequiredService<IStoragePipeline>(), async readCount =>
            {
                if (readCount == 1)
                {
                    await ExecuteGeneratePreviewJobAsync();
                }
            });

            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);

            Assert.That(storage.ReadCount, Is.EqualTo(1));
            Assert.That((await GetFileManifestByNodeFileIdAsync(older.Id)).SmallFilePreviewHash, Is.Not.Null);
            Assert.That((await GetFileManifestByNodeFileIdAsync(newer.Id)).SmallFilePreviewHash, Is.Not.Null);
        }

        [Test]
        public async Task PreviewPipeline_RepeatedConflicts_StopAfterOneRetryAndDiscardPendingChanges()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, "busy.txt", "text/plain", "Busy content"u8.ToArray());
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            int originalChunkCount = await dbContext.Chunks.CountAsync();
            CallbackStoragePipeline storage = new(scope.ServiceProvider.GetRequiredService<IStoragePipeline>(), async readCount =>
                await UpdateFileManifestAsync(file.Id, manifest =>
                    manifest.SmallFilePreviewHashEncrypted = [(byte)readCount]));

            await ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage);

            Assert.That(storage.ReadCount, Is.EqualTo(2));
            Assert.That(dbContext.ChangeTracker.Entries(), Is.Empty);
            Assert.That(await dbContext.Chunks.CountAsync(), Is.EqualTo(originalChunkCount));
            FileManifest manifest = await LoadFileManifestAsync(dbContext, file.Id);
            Assert.That(manifest.SmallFilePreviewHash, Is.Null);
            Assert.That(manifest.PreviewGenerationError, Is.Null);
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                .RequireValid(dbContext, manifest, "test.preview-conflicts");
        }

        private static async Task ExecutePreviewWithStorageAsync(IServiceProvider services, IStoragePipeline storage)
        {
            FilePreviewRenderer renderer = ActivatorUtilities.CreateInstance<FilePreviewRenderer>(services, storage);
            GeneratePreviewJob job = ActivatorUtilities.CreateInstance<GeneratePreviewJob>(services, storage, renderer);
            await job.Execute(null!);
        }
    }
}
