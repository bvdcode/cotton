// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using static Cotton.Server.IntegrationTests.Helpers.PreviewFixtures;

namespace Cotton.Server.IntegrationTests
{
    public class PreviewSvgTests : PreviewTestBase
    {
        [Test]
        public async Task PreviewPipeline_SvgWithEditorMetadata_GeneratesBothSizesAndStoresVersion()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            byte[] svg = Encoding.UTF8.GetBytes("""
                <svg xmlns="http://www.w3.org/2000/svg" xmlns:editor="urn:editor" width="400" height="200">
                  <editor:namedview /><namedview />
                  <rect x="20" y="20" width="360" height="160" fill="red" />
                  <text x="40" y="120" font-family="Canada1500" font-size="50" fill="white">Привет</text>
                </svg>
                """);
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "poster.svg", "image/svg+xml", svg);

            await Pipeline.ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(result.SmallFilePreviewHash, Is.Not.Null);
                Assert.That(result.SmallFilePreviewHashEncrypted, Is.Not.Null);
                Assert.That(result.LargeFilePreviewHash, Is.Not.Null);
                Assert.That(result.PreviewGenerationError, Is.Null);
            });
            byte[] small = await Pipeline.ReadPreviewBlobAsync(result.SmallFilePreviewHash!);
            byte[] large = await Pipeline.ReadPreviewBlobAsync(result.LargeFilePreviewHash!);
            AssertWebpSignature(small);
            AssertWebpSignature(large);
            Assert.That(GetImageSize(small), Is.EqualTo((200, 100)));
            Assert.That(GetImageSize(large), Is.EqualTo((2560, 1280)));

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await PreviewTestPipeline.LoadFileManifestAsync(dbContext, file.Id);
            Assert.That(manifest.PreviewGeneratorId, Is.EqualTo(new SvgPreviewGenerator().Id));
            Assert.That(manifest.PreviewGeneratorVersion, Is.EqualTo(new SvgPreviewGenerator().Version));
        }

        [Test]
        public async Task PreviewPipeline_UnsupportedSvg_RecordsFailureAndDoesNotRequeue()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            byte[] svg = Encoding.UTF8.GetBytes("""
                <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><foreignObject /></svg>
                """);
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "unsupported.svg", "image/svg+xml", svg);

            await Pipeline.ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(result.SmallFilePreviewHash, Is.Null);
                Assert.That(result.PreviewGenerationError, Is.Not.Null.And.Not.Empty);
            });
            Assert.That(await Pipeline.GetPendingPreviewIdsAsync(), Does.Not.Contain(file.FileManifestId));
        }
    }
}
