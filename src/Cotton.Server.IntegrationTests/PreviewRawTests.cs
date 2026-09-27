// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using static Cotton.Server.IntegrationTests.Helpers.PreviewFixtures;

namespace Cotton.Server.IntegrationTests
{
    public class PreviewRawTests : PreviewTestBase
    {
        [Test]
        public async Task PreviewPipeline_InvalidRaw_RecordsFailureWithoutPreview()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(
                root.Id, "invalid.cr3", "application/octet-stream", "not a camera RAW file"u8.ToArray());

            await Pipeline.ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState manifest = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
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

            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            byte[] fileBytes = await File.ReadAllBytesAsync(sourcePath);
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "photo.cr3", "application/octet-stream", fileBytes);

            await Pipeline.ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState manifest = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.That(manifest.PreviewGenerationError, Is.Null);
            Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
            Assert.That(manifest.LargeFilePreviewHash, Is.Not.Null);

            byte[] small = await Pipeline.ReadPreviewBlobAsync(manifest.SmallFilePreviewHash!);
            byte[] large = await Pipeline.ReadPreviewBlobAsync(manifest.LargeFilePreviewHash!);
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
