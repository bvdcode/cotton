// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [Test]
        public async Task PreviewPipeline_InvalidRaw_RecordsFailureWithoutPreview()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(
                root.Id, "invalid.cr3", "application/octet-stream", "not a camera RAW file"u8.ToArray());

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState manifest = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(manifest.SmallFilePreviewHash, Is.Null);
            Assert.That(manifest.LargeFilePreviewHash, Is.Null);
            Assert.That(manifest.PreviewGenerationError, Is.Not.Null);
        }

        [Test]
        public async Task PreviewPipeline_RealCr3_GeneratesSmallAndLargePreviews()
        {
            string? sourcePath = Environment.GetEnvironmentVariable("COTTON_TEST_CR3_FILE");
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                Assert.Ignore("Set COTTON_TEST_CR3_FILE to a real CR3 file for this test.");
            }

            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            byte[] fileBytes = await File.ReadAllBytesAsync(sourcePath);
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, "photo.cr3", "application/octet-stream", fileBytes);

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState manifest = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(manifest.PreviewGenerationError, Is.Null);
            Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
            Assert.That(manifest.LargeFilePreviewHash, Is.Not.Null);

            byte[] small = await ReadPreviewBlobAsync(manifest.SmallFilePreviewHash!);
            byte[] large = await ReadPreviewBlobAsync(manifest.LargeFilePreviewHash!);
            AssertWebpSignature(small);
            AssertWebpSignature(large);
            var (smallWidth, smallHeight) = GetImageSize(small);
            var (largeWidth, largeHeight) = GetImageSize(large);
            Assert.Multiple(() =>
            {
                Assert.That(Math.Max(smallWidth, smallHeight),
                    Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));
                Assert.That(Math.Max(largeWidth, largeHeight),
                    Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultLargePreviewSize));
                Assert.That((long)largeWidth * largeHeight, Is.GreaterThan((long)smallWidth * smallHeight));
            });

            string downloadLink = (await _client!.GetStringAsync($"/api/v1/files/{file.Id}/download-link"))
                .Trim().Trim('"');
            using HttpResponseMessage served = await _client.GetAsync($"{downloadLink}&preview=true");
            served.EnsureSuccessStatusCode();
            Assert.That(served.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
            AssertWebpSignature(await served.Content.ReadAsByteArrayAsync());
        }
    }
}
