// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [Test]
        public async Task PreviewPipeline_PdfFile_GeneratesSmallPreviewOnly_AndReturnsWebpFromEndpoint()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            byte[] pdfBytes = CreateSinglePagePdfBytes("Preview PDF E2E");

            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, "document.pdf", "application/pdf", pdfBytes);

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

            var (width, height) = GetImageSize(smallPreview);
            Assert.That(Math.Max(width, height), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));

            HttpResponseMessage response = await _client!.GetAsync($"{PreviewRouteBase}/{GetPreviewHashEncryptedHex(manifest.Id, manifest.SmallFilePreviewHashEncrypted)}");
            response.EnsureSuccessStatusCode();
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
        }

        [Test]
        public async Task PreviewPipeline_UnsupportedType_DoesNotGeneratePreview()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            byte[] bytes = Encoding.UTF8.GetBytes("raw bytes that should not get preview");

            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, "raw.bin", "application/octet-stream", bytes);

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState manifest = await GetFileManifestByNodeFileIdAsync(createdFile.Id);
            Assert.Multiple(() =>
            {
                Assert.That(manifest.SmallFilePreviewHash, Is.Null);
                Assert.That(manifest.SmallFilePreviewHashEncrypted, Is.Null);
                Assert.That(manifest.LargeFilePreviewHash, Is.Null);
                Assert.That(manifest.PreviewGenerationError, Is.Null);
            });

            NodeFileManifestDto listedFile = await GetNodeFileAsync(root.Id, "raw.bin");
            Assert.That(listedFile.PreviewHashEncryptedHex, Is.Null);
        }

        [Test]
        public async Task PreviewPipeline_ExternalFixtures_GeneratesPreviewsForAllProvidedFiles_WhenDirectoryConfigured()
        {
            string fixturesDir = ResolveExternalFixturesDir();
            Directory.CreateDirectory(fixturesDir);
            EnsureDefaultFixturesExist(fixturesDir);

            string[] files = [.. Directory
                .GetFiles(fixturesDir)
                .Where(filePath => ResolveContentType(filePath) is not null)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)];

            if (files.Length == 0)
            {
                Assert.Fail($"No supported preview fixtures found in '{fixturesDir}'.");
            }

            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            List<FixtureUpload> uploads = [];

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string contentType = ResolveContentType(filePath)!;

                byte[] source = await File.ReadAllBytesAsync(filePath);
                NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, fileName, contentType, source);

                uploads.Add(new FixtureUpload(
                    NodeFileId: createdFile.Id,
                    FileName: fileName,
                    ContentType: contentType,
                    SourceLength: source.Length,
                    ExpectLargePreview: ExpectsLargePreview(contentType)));
            }

            await ExecuteGeneratePreviewJobAsync();

            foreach (FixtureUpload upload in uploads)
            {
                FileManifestPreviewState manifest = await GetFileManifestByNodeFileIdAsync(upload.NodeFileId);

                Assert.Multiple(() =>
                {
                    Assert.That(manifest.PreviewGenerationError, Is.Null, $"Preview generation failed for fixture {upload.FileName}");
                    Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null, $"Small preview was not generated for fixture {upload.FileName}");
                });

                if (upload.ExpectLargePreview)
                {
                    Assert.That(manifest.LargeFilePreviewHash, Is.Not.Null, $"Large preview expected but missing for fixture {upload.FileName}");
                }
                else
                {
                    Assert.That(manifest.LargeFilePreviewHash, Is.Null, $"Large preview is not expected for fixture {upload.FileName}");
                }

                byte[] smallPreview = await ReadPreviewBlobAsync(manifest.SmallFilePreviewHash!);
                AssertWebpSignature(smallPreview);
                var (smallWidth, smallHeight) = GetImageSize(smallPreview);
                Assert.That(Math.Max(smallWidth, smallHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));

                if (manifest.LargeFilePreviewHash is not null)
                {
                    byte[] largePreview = await ReadPreviewBlobAsync(manifest.LargeFilePreviewHash);
                    AssertWebpSignature(largePreview);

                    var (largeWidth, largeHeight) = GetImageSize(largePreview);
                    Assert.That(Math.Max(largeWidth, largeHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultLargePreviewSize));
                    Assert.That(largeWidth * largeHeight, Is.GreaterThanOrEqualTo(smallWidth * smallHeight));
                    Assert.That(largePreview.Length, Is.GreaterThan(0));
                }

                NodeFileManifestDto listedFile = await GetNodeFileAsync(root.Id, upload.FileName);
                Assert.That(listedFile.PreviewHashEncryptedHex, Is.EqualTo(GetPreviewHashEncryptedHex(manifest.Id, manifest.SmallFilePreviewHashEncrypted)));

                HttpResponseMessage response = await _client!.GetAsync($"{PreviewRouteBase}/{listedFile.PreviewHashEncryptedHex}");
                response.EnsureSuccessStatusCode();
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
            }
        }

    }
}
