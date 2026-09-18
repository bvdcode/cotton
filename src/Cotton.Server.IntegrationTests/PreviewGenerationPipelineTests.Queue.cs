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
            List<FileManifest> result = await PreviewQueueLoader.LoadNextAsync(dbContext, 1, known, CancellationToken.None);

            Assert.That(result.Select(manifest => manifest.Id), Is.EqualTo(new[] { older.FileManifestId }));
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
            List<FileManifest> pending = await PreviewQueueLoader.LoadNextAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None);
            FileManifest manifest = pending.Single(item => item.Id == file.FileManifestId);
            FilePreviewRenderer renderer = scope.ServiceProvider.GetRequiredService<FilePreviewRenderer>();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            Assert.ThrowsAsync<OperationCanceledException>(() => renderer.RenderAsync(manifest, cancellation.Token));
            Assert.That(manifest.PreviewGenerationError, Is.Null);
        }
    }
}
