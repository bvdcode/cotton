// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [Test]
        public async Task PreviewPipeline_TextFile_GeneratesSmallPreviewOnly_AndServesCachedWebp()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            byte[] textBytes = Encoding.UTF8.GetBytes("Hello preview pipeline!\nThis is text content for small preview generation.");

            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, "notes.txt", "text/plain", textBytes);

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState manifest = await GetFileManifestByNodeFileIdAsync(createdFile.Id);

            Assert.Multiple(() =>
            {
                Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
                Assert.That(manifest.SmallFilePreviewHashEncrypted, Is.Not.Null);
                Assert.That(manifest.LargeFilePreviewHash, Is.Null);
                Assert.That(manifest.PreviewGenerationError, Is.Null);
            });

            byte[] smallPreview = await ReadPreviewBlobAsync(manifest.SmallFilePreviewHash!);
            AssertWebpSignature(smallPreview);

            var (smallWidth, smallHeight) = GetImageSize(smallPreview);
            Assert.That(Math.Max(smallWidth, smallHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));

            NodeFileManifestDto listedFile = await GetNodeFileAsync(root.Id, "notes.txt");
            Assert.That(listedFile.PreviewHashEncryptedHex, Is.EqualTo(GetPreviewHashEncryptedHex(manifest.Id, manifest.SmallFilePreviewHashEncrypted)));

            string previewUrl = $"{PreviewRouteBase}/{listedFile.PreviewHashEncryptedHex}";
            HttpResponseMessage previewResponse = await _client!.GetAsync(previewUrl);
            previewResponse.EnsureSuccessStatusCode();

            Assert.That(previewResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
            string? etag = previewResponse.Headers.ETag?.Tag;
            Assert.That(etag, Is.Not.Null.And.Not.Empty);

            byte[] previewBytesFromApi = await previewResponse.Content.ReadAsByteArrayAsync();
            AssertWebpSignature(previewBytesFromApi);

            HttpResponseMessage rawTokenResponse = await _client!.GetAsync($"{PreviewRouteBase}/{Convert.ToHexStringLower(manifest.SmallFilePreviewHashEncrypted!)}");
            Assert.That(rawTokenResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            using HttpRequestMessage conditional = new(HttpMethod.Get, previewUrl);
            conditional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag!));

            using HttpResponseMessage strongNotModified = await _client.SendAsync(conditional);
            Assert.That(strongNotModified.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));

            using HttpRequestMessage weakConditional = new(HttpMethod.Get, previewUrl);
            weakConditional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag!, isWeak: true));

            using HttpResponseMessage weakNotModified = await _client.SendAsync(weakConditional);
            Assert.That(weakNotModified.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));

            using HttpRequestMessage anyConditional = new(HttpMethod.Get, previewUrl);
            anyConditional.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);

            using HttpResponseMessage anyNotModified = await _client.SendAsync(anyConditional);
            Assert.That(anyNotModified.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
        }

        [Test]
        public async Task PreviewPipeline_StaleGeneratorVersion_RegeneratesPreview()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(
                root.Id,
                "stale-preview.txt",
                "text/plain",
                Encoding.UTF8.GetBytes("stale preview"));
            await ExecuteGeneratePreviewJobAsync();

            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                FileManifest manifest = await LoadFileManifestAsync(dbContext, createdFile.Id);
                manifest.PreviewGeneratorVersion = -1;
                await dbContext.SaveChangesAsync();
            }

            await ExecuteGeneratePreviewJobAsync();

            await using AsyncServiceScope verificationScope = _factory.Services.CreateAsyncScope();
            CottonDbContext verificationContext = verificationScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            int actualVersion = await verificationContext.NodeFiles
                .Where(nodeFile => nodeFile.Id == createdFile.Id)
                .Select(nodeFile => nodeFile.FileManifest.PreviewGeneratorVersion)
                .SingleAsync();
            int expectedVersion = PreviewGeneratorProvider
                .GetGeneratorVersionsByContentType()["text/plain"];
            Assert.That(actualVersion, Is.EqualTo(expectedVersion));
        }

        [Test]
        public async Task PreviewPipeline_LargeImage_GeneratesSmallAndLarge_WithExpectedDimensions_AndCompression()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            byte[] sourceImage = CreateGradientPngBytes(width: 2200, height: 1200);

            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, "photo.png", "image/png", sourceImage);

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState manifest = await GetFileManifestByNodeFileIdAsync(createdFile.Id);

            Assert.Multiple(() =>
            {
                Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
                Assert.That(manifest.SmallFilePreviewHashEncrypted, Is.Not.Null);
                Assert.That(manifest.LargeFilePreviewHash, Is.Not.Null);
                Assert.That(manifest.PreviewGenerationError, Is.Null);
            });

            byte[] smallPreview = await ReadPreviewBlobAsync(manifest.SmallFilePreviewHash!);
            byte[] largePreview = await ReadPreviewBlobAsync(manifest.LargeFilePreviewHash!);

            Assert.Multiple(() =>
            {
                AssertWebpSignature(smallPreview);
                AssertWebpSignature(largePreview);
                Assert.That(smallPreview.Length, Is.GreaterThan(0));
                Assert.That(largePreview.Length, Is.GreaterThan(0));
            });

            var (smallWidth, smallHeight) = GetImageSize(smallPreview);
            var (largeWidth, largeHeight) = GetImageSize(largePreview);

            Assert.Multiple(() =>
            {
                Assert.That(Math.Max(smallWidth, smallHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));
                Assert.That(Math.Max(largeWidth, largeHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultLargePreviewSize));
                Assert.That((largeWidth * largeHeight), Is.GreaterThan(smallWidth * smallHeight));
            });

            Chunk smallChunk = await GetChunkByHashAsync(manifest.SmallFilePreviewHash!);
            Chunk largeChunk = await GetChunkByHashAsync(manifest.LargeFilePreviewHash!);

            Assert.Multiple(() =>
            {
                Assert.That(smallChunk.PlainSizeBytes, Is.EqualTo(smallPreview.Length));
                Assert.That(smallChunk.StoredSizeBytes, Is.GreaterThan(0));
                Assert.That(largeChunk.PlainSizeBytes, Is.EqualTo(largePreview.Length));
                Assert.That(largeChunk.StoredSizeBytes, Is.GreaterThan(0));
            });
        }

    }
}
