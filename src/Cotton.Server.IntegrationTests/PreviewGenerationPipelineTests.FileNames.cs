// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [TestCase("notes.txt")]
        [TestCase("styles.css")]
        [TestCase("index.html")]
        [TestCase("script.ts")]
        public async Task PreviewPipeline_UsesFilenameInsteadOfStoredMime(string fileName)
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, fileName,
                "application/octet-stream", Encoding.UTF8.GetBytes("Filename based preview"));
            await UpdateFileManifestAsync(file.Id, manifest => manifest.ContentType = "application/x-unsupported");

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(result.SmallFilePreviewHash, Is.Not.Null);
            Assert.That(result.PreviewGenerationError, Is.Null);
        }

        [Test]
        public async Task PreviewPipeline_FailedImageAttempt_TriesTextAlias()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            byte[] bytes = Encoding.UTF8.GetBytes("Text with an incorrect image extension");
            NodeFileManifestDto image = await UploadAndCreateFileAsync(root.Id, "wrong.png", "image/png", bytes);
            NodeFileManifestDto text = await UploadAndCreateFileAsync(root.Id, "right.txt", "text/plain", bytes);
            Assert.That(image.FileManifestId, Is.EqualTo(text.FileManifestId));

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await GetFileManifestByNodeFileIdAsync(image.Id);
            Assert.Multiple(() =>
            {
                Assert.That(result.SmallFilePreviewHash, Is.Not.Null);
                Assert.That(result.LargeFilePreviewHash, Is.Null);
                Assert.That(result.PreviewGenerationError, Is.Null);
            });
            AssertWebpSignature(await ReadPreviewBlobAsync(result.SmallFilePreviewHash!));
        }

        [Test]
        public async Task PreviewPipeline_ImageAlias_TakesPriorityOverTextAlias()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            byte[] bytes = CreateGradientPngBytes(96, 64);
            NodeFileManifestDto text = await UploadAndCreateFileAsync(root.Id, "wrong.txt", "text/plain", bytes);
            NodeFileManifestDto image = await UploadAndCreateFileAsync(root.Id, "right.png", "image/png", bytes);
            Assert.That(image.FileManifestId, Is.EqualTo(text.FileManifestId));

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await GetFileManifestByNodeFileIdAsync(image.Id);
            Assert.That(result.LargeFilePreviewHash, Is.Not.Null);
            Assert.That(result.PreviewGenerationError, Is.Null);
        }

        [TestCase("opaque-name")]
        [TestCase("invalid.png")]
        public async Task PreviewPipeline_FailedPreview_RenameRequeues_ReadyPreviewIsPreserved(string originalName)
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, originalName,
                "application/octet-stream", Encoding.UTF8.GetBytes("Rename into a supported text type"));

            await ExecuteGeneratePreviewJobAsync();
            FileManifestPreviewState failed = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(failed.PreviewGenerationError, Is.Not.Null);
            Assert.That(await GetPendingPreviewIdsAsync(), Does.Not.Contain(file.FileManifestId));

            await RenamePreviewFileAsync(file.Id, "renamed.txt");
            FileManifestPreviewState reset = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(reset.PreviewGenerationError, Is.Null);
            Assert.That(await GetPendingPreviewIdsAsync(), Does.Contain(file.FileManifestId));

            await ExecuteGeneratePreviewJobAsync();
            FileManifestPreviewState ready = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(ready.SmallFilePreviewHash, Is.Not.Null);

            await RenamePreviewFileAsync(file.Id, "renamed-again");
            FileManifestPreviewState renamed = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(renamed.SmallFilePreviewHashEncrypted, Is.EqualTo(ready.SmallFilePreviewHashEncrypted));
            Assert.That(await GetPendingPreviewIdsAsync(), Does.Not.Contain(file.FileManifestId));
        }

        [Test]
        public async Task PreviewPipeline_NewAlias_RequeuesFailedManifest()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            byte[] bytes = CreateGradientPngBytes(48, 32);
            NodeFileManifestDto original = await UploadAndCreateFileAsync(root.Id, "opaque-name", "application/octet-stream", bytes);
            await ExecuteGeneratePreviewJobAsync();
            Assert.That((await GetFileManifestByNodeFileIdAsync(original.Id)).PreviewGenerationError, Is.Not.Null);

            NodeFileManifestDto alias = await UploadAndCreateFileAsync(root.Id, "photo.png", "image/png", bytes);
            Assert.That(alias.FileManifestId, Is.EqualTo(original.FileManifestId));
            Assert.That((await GetFileManifestByNodeFileIdAsync(original.Id)).PreviewGenerationError, Is.Null);
            await ExecuteGeneratePreviewJobAsync();

            Assert.That((await GetFileManifestByNodeFileIdAsync(original.Id)).LargeFilePreviewHash, Is.Not.Null);
        }

        private async Task RenamePreviewFileAsync(Guid fileId, string name)
        {
            using HttpResponseMessage response = await _client!.PatchAsJsonAsync(
                $"/api/v1/files/{fileId}/rename", new RenameFileRequestDto { Name = name });
            response.EnsureSuccessStatusCode();
        }

        [TestCase("MOVE")]
        [TestCase("COPY")]
        public async Task PreviewPipeline_WebDavNewName_RequeuesFailedManifest(string method)
        {
            string token = await LoginAsync();
            SetBearer(token);
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, "webdav-source",
                "application/octet-stream", Encoding.UTF8.GetBytes("WebDAV rename preview"));
            await ExecuteGeneratePreviewJobAsync();
            Assert.That((await GetFileManifestByNodeFileIdAsync(file.Id)).PreviewGenerationError, Is.Not.Null);

            string webDavToken = await _client!.GetStringAsync("/api/v1/auth/webdav/token");
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), "/api/v1/webdav/webdav-source");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{webDavToken}")));
            request.Headers.Add("Destination", "/api/v1/webdav/webdav-target.txt");
            request.Headers.Add("Overwrite", "F");
            using HttpResponseMessage response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            Assert.That((await GetFileManifestByNodeFileIdAsync(file.Id)).PreviewGenerationError, Is.Null);
            await ExecuteGeneratePreviewJobAsync();
            Assert.That((await GetFileManifestByNodeFileIdAsync(file.Id)).SmallFilePreviewHash, Is.Not.Null);
        }

        private async Task<Guid[]> GetPendingPreviewIdsAsync()
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            List<Guid> items = await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None);
            return [.. items];
        }
    }
}
