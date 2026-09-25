// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [Test]
        public async Task PreviewPipeline_GeneratesStickerPosterFromExistingOctetStreamFile()
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto file = await UploadAndCreateFileAsync(root.Id, "AnimatedSticker.tgs",
                "application/octet-stream", await TgsTestDocument.CreateAsync());
            await UpdateFileManifestAsync(file.Id, manifest =>
            {
                manifest.PreviewGenerationError = "No preview generator matches the file names.";
                manifest.PreviewGeneratorVersion = PreviewGeneratorProvider.FailedAttemptVersion ^ 1;
            });
            Assert.That(await GetPendingPreviewIdsAsync(), Does.Contain(file.FileManifestId));

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState result = await GetFileManifestByNodeFileIdAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(result.SmallFilePreviewHash, Is.Not.Null);
                Assert.That(result.PreviewGenerationError, Is.Null);
                Assert.That(result.LargeFilePreviewHash, Is.Null);
            });
            AssertWebpSignature(await ReadPreviewBlobAsync(result.SmallFilePreviewHash!));
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await LoadFileManifestAsync(dbContext, file.Id);
            Assert.That(manifest.PreviewGeneratorId, Is.EqualTo(new TgsPreviewGenerator().Id));
            Assert.That(FileContentTypeResolver.ResolveFromFileName("AnimatedSticker.tgs"),
                Is.EqualTo(AnimatedStickerContentTypes.Tgs));
        }
    }
}
