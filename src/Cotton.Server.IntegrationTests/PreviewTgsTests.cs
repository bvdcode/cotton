// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;

using static Cotton.Server.IntegrationTests.Helpers.PreviewFixtures;

namespace Cotton.Server.IntegrationTests
{
    public class PreviewTgsTests : PreviewTestBase
    {
        [Test]
        public async Task PreviewPipeline_GeneratesStickerPosterFromExistingOctetStreamFile()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "AnimatedSticker.tgs",
                "application/octet-stream", await TgsTestDocument.CreateAsync());
            await Pipeline.UpdateFileManifestAsync(file.Id, manifest =>
            {
                manifest.PreviewGenerationError = "No preview generator matches the file names.";
                manifest.PreviewGeneratorVersion = PreviewGeneratorProvider.FailedAttemptVersion ^ 1;
            });
            Assert.That(await Pipeline.GetPendingPreviewIdsAsync(), Does.Contain(file.FileManifestId));

            await Pipeline.ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await Pipeline.GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(result.SmallFilePreviewHash, Is.Not.Null);
                Assert.That(result.PreviewGenerationError, Is.Null);
                Assert.That(result.LargeFilePreviewHash, Is.Null);
            });
            AssertWebpSignature(await Pipeline.ReadPreviewBlobAsync(result.SmallFilePreviewHash!));
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await PreviewTestPipeline.LoadFileManifestAsync(dbContext, file.Id);
            Assert.That(manifest.PreviewGeneratorId, Is.EqualTo(new TgsPreviewGenerator().Id));
            Assert.That(FileContentTypeResolver.ResolveFromFileName("AnimatedSticker.tgs"),
                Is.EqualTo(AnimatedStickerContentTypes.Tgs));
        }
    }
}
