// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Previews;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [Test]
        public async Task PreviewQueue_ExcludesKnownItemsBeforeApplyingLimit()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto older = await UploadAndCreateFileAsync(root.Id, "older.txt", "text/plain", Encoding.UTF8.GetBytes("older"));
            NodeFileManifestDto newer = await UploadAndCreateFileAsync(root.Id, "newer.txt", "text/plain", Encoding.UTF8.GetBytes("newer"));

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            HashSet<Guid> known = [newer.FileManifestId];
            List<Guid> result = await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 1, known, CancellationToken.None);

            Assert.That(result, Is.EqualTo(new[] { older.FileManifestId }));
            Assert.That(dbContext.ChangeTracker.Entries(), Is.Empty);
        }

        [Test]
        public async Task PreviewQueue_ContinuesThroughMultipleBatches()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto original = await UploadAndCreateFileAsync(root.Id, "opaque", "application/octet-stream", "Original content"u8.ToArray());
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile source = await dbContext.NodeFiles.SingleAsync(file => file.Id == original.Id);
            for (int index = 0; index < 100; index++)
            {
                NodeFile file = new()
                {
                    NodeId = source.NodeId,
                    OwnerId = source.OwnerId,
                    FileManifest = new FileManifest
                    {
                        ProposedContentHash = Hasher.HashData(Encoding.UTF8.GetBytes($"Queued content {index}")),
                        ContentType = string.Empty,
                        SizeBytes = 1,
                    },
                };
                file.SetName($"opaque-{index}");
                dbContext.NodeFiles.Add(file);
            }
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
            PerfTracker perf = ActivatorUtilities.CreateInstance<PerfTracker>(scope.ServiceProvider);
            GeneratePreviewJob job = ActivatorUtilities.CreateInstance<GeneratePreviewJob>(scope.ServiceProvider, perf);

            await job.Execute(null!);

            Assert.That(dbContext.ChangeTracker.Entries(), Is.Empty);
            Assert.That(await dbContext.FileManifests.CountAsync(manifest =>
                manifest.PreviewGeneratorVersion == PreviewGeneratorProvider.FailedAttemptVersion
                    && manifest.PreviewGenerationError != null), Is.EqualTo(101));
        }

        [Test]
        public async Task PreviewQueue_EncryptedAlias_DoesNotSupplyCandidates()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            byte[] bytes = CreateGradientPngBytes(48, 32);
            NodeFileManifestDto opaque = await UploadAndCreateFileAsync(root.Id, "opaque", "image/png", bytes);
            NodeFileManifestDto encrypted = await UploadAndCreateFileAsync(root.Id, "encrypted.png", "image/png", bytes);
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                NodeFile file = await dbContext.NodeFiles.SingleAsync(file => file.Id == encrypted.Id);
                file.Metadata = new Dictionary<string, string> { ["isClientEncrypted"] = "true" };
                await dbContext.SaveChangesAsync();
            }

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await GetFileManifestByNodeFileIdAsync(opaque.Id);
            Assert.That(result.SmallFilePreviewHash, Is.Null);
            Assert.That(result.PreviewGenerationError, Is.Not.Null);
        }

        [Test]
        public async Task PreviewRenderer_Cancellation_DoesNotRecordFailure()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, "cancel.txt", "text/plain", Encoding.UTF8.GetBytes("cancelled"));
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = (await PreviewQueueLoader.LoadItemAsync(dbContext, file.FileManifestId, CancellationToken.None))!;
            FilePreviewRenderer renderer = scope.ServiceProvider.GetRequiredService<FilePreviewRenderer>();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            Assert.ThrowsAsync<OperationCanceledException>(() => renderer.RenderAsync(manifest, cancellation.Token));
            Assert.That(manifest.PreviewGenerationError, Is.Null);
        }

        [Test]
        public async Task PreviewQueue_OutOfMemory_DoesNotRecordPermanentFailure()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(
                root.Id, "photo.png", "image/png", CreateGradientPngBytes(48, 32));

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            CallbackStoragePipeline storage = new(scope.ServiceProvider.GetRequiredService<IStoragePipeline>(),
                _ => throw new OutOfMemoryException());

            Assert.ThrowsAsync<OutOfMemoryException>(() => ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage));
            dbContext.ChangeTracker.Clear();
            FileManifest manifest = await LoadFileManifestAsync(dbContext, file.Id);
            Assert.That(manifest.PreviewGenerationError, Is.Null);
            Assert.That(await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 10, new HashSet<Guid>(), CancellationToken.None),
                Does.Contain(file.FileManifestId));

            await ExecuteGeneratePreviewJobAsync();
            Assert.That((await GetFileManifestByNodeFileIdAsync(file.Id)).SmallFilePreviewHash, Is.Not.Null);
        }

        [Test]
        public async Task PreviewQueue_OutOfMemoryDuringStorage_DoesNotRecordPermanentFailure()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(
                root.Id, "photo.png", "image/png", CreateGradientPngBytes(48, 32));

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            CallbackStoragePipeline storage = new(scope.ServiceProvider.GetRequiredService<IStoragePipeline>(),
                _ => Task.CompletedTask, _ => throw new OutOfMemoryException());

            Assert.ThrowsAsync<OutOfMemoryException>(() => ExecutePreviewWithStorageAsync(scope.ServiceProvider, storage));
            dbContext.ChangeTracker.Clear();
            FileManifest manifest = await LoadFileManifestAsync(dbContext, file.Id);
            Assert.That(manifest.PreviewGenerationError, Is.Null);
            Assert.That(manifest.SmallFilePreviewHash, Is.Null);
            Assert.That(await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 10, new HashSet<Guid>(), CancellationToken.None),
                Does.Contain(file.FileManifestId));
        }

        [Test]
        public async Task PreviewQueue_PreviousOutOfMemoryFailure_IsRetried()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(
                root.Id, "photo.png", "image/png", CreateGradientPngBytes(48, 32));

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await LoadFileManifestAsync(dbContext, file.Id);
            manifest.PreviewGenerationError =
                "All matching preview generators failed. (Exception of type 'System.OutOfMemoryException' was thrown.)";
            manifest.PreviewGeneratorVersion = PreviewGeneratorProvider.FailedAttemptVersion;
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();

            Assert.That(await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 10, new HashSet<Guid>(), CancellationToken.None),
                Does.Contain(file.FileManifestId));

            await ExecuteGeneratePreviewJobAsync();
            FileManifestPreviewState result = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(result.PreviewGenerationError, Is.Null);
            Assert.That(result.SmallFilePreviewHash, Is.Not.Null);
        }
    }
}
