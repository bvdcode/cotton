// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

using static Cotton.Server.IntegrationTests.Helpers.PreviewFixtures;

namespace Cotton.Server.IntegrationTests
{
    public class PreviewFileNamesTests : PreviewTestBase
    {
        [TestCase("notes.txt")]
        [TestCase("styles.css")]
        [TestCase("index.html")]
        [TestCase("script.ts")]
        public async Task PreviewPipeline_UsesFilenameInsteadOfStoredMime(string fileName)
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, fileName,
                "application/octet-stream", Encoding.UTF8.GetBytes("Filename based preview"));
            await Pipeline.UpdateFileManifestAsync(file.Id, manifest => manifest.ContentType = "application/x-unsupported");

            await Pipeline.ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(result.SmallFilePreviewHash, Is.Not.Null);
            Assert.That(result.PreviewGenerationError, Is.Null);
        }

        [Test]
        public async Task PreviewPipeline_FailedImageAttempt_TriesTextAlias()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            byte[] bytes = Encoding.UTF8.GetBytes("Text with an incorrect image extension");
            NodeFileManifestDto image = await Pipeline.UploadAndCreateFileAsync(root.Id, "wrong.png", "image/png", bytes);
            NodeFileManifestDto text = await Pipeline.UploadAndCreateFileAsync(root.Id, "right.txt", "text/plain", bytes);
            Assert.That(image.FileManifestId, Is.EqualTo(text.FileManifestId));

            await Pipeline.ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await Pipeline.GetFileManifestByNodeFileIdAsync(image.Id);
            Assert.Multiple(() =>
            {
                Assert.That(result.SmallFilePreviewHash, Is.Not.Null);
                Assert.That(result.LargeFilePreviewHash, Is.Null);
                Assert.That(result.PreviewGenerationError, Is.Null);
            });
            AssertWebpSignature(await Pipeline.ReadPreviewBlobAsync(result.SmallFilePreviewHash!));
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await PreviewTestPipeline.LoadFileManifestAsync(dbContext, image.Id);
            Assert.That(manifest.PreviewGeneratorId, Is.EqualTo(new TextPreviewGenerator().Id));
            Assert.That(manifest.PreviewGeneratorVersion, Is.EqualTo(new TextPreviewGenerator().Version));
            Assert.That(await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None), Is.Empty);
        }

        [Test]
        public async Task PreviewPipeline_ImageAlias_TakesPriorityOverTextAlias()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            byte[] bytes = CreateGradientPngBytes(96, 64);
            NodeFileManifestDto text = await Pipeline.UploadAndCreateFileAsync(root.Id, "wrong.txt", "text/plain", bytes);
            NodeFileManifestDto image = await Pipeline.UploadAndCreateFileAsync(root.Id, "right.png", "image/png", bytes);
            Assert.That(image.FileManifestId, Is.EqualTo(text.FileManifestId));

            await Pipeline.ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await Pipeline.GetFileManifestByNodeFileIdAsync(image.Id);
            Assert.That(result.LargeFilePreviewHash, Is.Not.Null);
            Assert.That(result.PreviewGenerationError, Is.Null);
        }

        [TestCase("opaque-name")]
        [TestCase("invalid.png")]
        public async Task PreviewPipeline_FailedPreview_RenameRequeues_ReadyPreviewIsPreserved(string originalName)
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, originalName,
                "application/octet-stream", Encoding.UTF8.GetBytes("Rename into a supported text type"));

            await Pipeline.ExecuteGeneratePreviewJobAsync();
            FileManifestPreviewState failed = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(failed.PreviewGenerationError, Is.Not.Null);
            Assert.That(await Pipeline.GetPendingPreviewIdsAsync(), Does.Not.Contain(file.FileManifestId));

            await Pipeline.RenamePreviewFileAsync(file.Id, "renamed.txt");
            FileManifestPreviewState reset = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(reset.PreviewGenerationError, Is.Null);
            Assert.That(await Pipeline.GetPendingPreviewIdsAsync(), Does.Contain(file.FileManifestId));

            await Pipeline.ExecuteGeneratePreviewJobAsync();
            FileManifestPreviewState ready = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(ready.SmallFilePreviewHash, Is.Not.Null);

            await Pipeline.RenamePreviewFileAsync(file.Id, "renamed-again");
            FileManifestPreviewState renamed = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(renamed.SmallFilePreviewHashEncrypted, Is.EqualTo(ready.SmallFilePreviewHashEncrypted));
            Assert.That(await Pipeline.GetPendingPreviewIdsAsync(), Does.Not.Contain(file.FileManifestId));
        }

        [Test]
        public async Task PreviewPipeline_NewAlias_RequeuesFailedManifest()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            byte[] bytes = CreateGradientPngBytes(48, 32);
            NodeFileManifestDto original = await Pipeline.UploadAndCreateFileAsync(root.Id, "opaque-name", "application/octet-stream", bytes);
            await Pipeline.ExecuteGeneratePreviewJobAsync();
            Assert.That((await Pipeline.GetFileManifestByNodeFileIdAsync(original.Id)).PreviewGenerationError, Is.Not.Null);

            NodeFileManifestDto alias = await Pipeline.UploadAndCreateFileAsync(root.Id, "photo.png", "image/png", bytes);
            Assert.That(alias.FileManifestId, Is.EqualTo(original.FileManifestId));
            Assert.That((await Pipeline.GetFileManifestByNodeFileIdAsync(original.Id)).PreviewGenerationError, Is.Null);
            await Pipeline.ExecuteGeneratePreviewJobAsync();

            Assert.That((await Pipeline.GetFileManifestByNodeFileIdAsync(original.Id)).LargeFilePreviewHash, Is.Not.Null);
        }

        [TestCase("MOVE")]
        [TestCase("COPY")]
        public async Task PreviewPipeline_WebDavNewName_RequeuesFailedManifest(string method)
        {
            string token = await Pipeline.LoginAsync();
            Pipeline.SetBearer(token);
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "webdav-source",
                "application/octet-stream", Encoding.UTF8.GetBytes("WebDAV rename preview"));
            await Pipeline.ExecuteGeneratePreviewJobAsync();
            Assert.That((await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id)).PreviewGenerationError, Is.Not.Null);

            string webDavToken = await _client!.GetStringAsync("/api/v1/auth/webdav/token");
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), "/api/v1/webdav/webdav-source");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{webDavToken}")));
            request.Headers.Add("Destination", "/api/v1/webdav/webdav-target.txt");
            request.Headers.Add("Overwrite", "F");
            using HttpResponseMessage response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            Assert.That((await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id)).PreviewGenerationError, Is.Null);
            await Pipeline.ExecuteGeneratePreviewJobAsync();
            Assert.That((await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id)).SmallFilePreviewHash, Is.Not.Null);
        }
    }
}
