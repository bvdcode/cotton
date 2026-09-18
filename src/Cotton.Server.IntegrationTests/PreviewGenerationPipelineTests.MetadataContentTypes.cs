// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task MetadataExtraction_TriesEachRelatedTypeOnce_AfterRejectedAttempt(bool throwUnavailable)
        {
            SetBearer(await LoginAsync());
            NodeDto root = await GetRootNodeAsync();
            byte[] content = CreateGradientPngBytes(32, 24);
            NodeFileManifestDto original = await UploadAndCreateFileAsync(root.Id, "opaque-name", "application/octet-stream", content);
            foreach (string name in new[] { "first.png", "second.PNG", "third.jpg", "fourth.jpeg" })
            {
                NodeFileManifestDto alias = await UploadAndCreateFileAsync(root.Id, name, "application/octet-stream", content);
                Assert.That(alias.FileManifestId, Is.EqualTo(original.FileManifestId));
            }
            RecordingContentMetadataExtractor extractor = new(["image/png", "image/jpeg"], (stream, attempt) =>
            {
                Assert.That(stream.Position, Is.Zero);
                if (attempt == 1)
                {
                    stream.Position = 1;
                    if (throwUnavailable)
                    {
                        throw new FileMetadataUnavailableException("Unsupported content for this type.");
                    }
                    return new Dictionary<string, string>();
                }
                return new Dictionary<string, string> { [FileContentMetadataKeys.ImageWidth] = "32" };
            });
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            ExtractFileManifestMetadataRequestHandler handler = new(dbContext,
                scope.ServiceProvider.GetRequiredService<IStoragePipeline>(),
                new FileContentMetadataExtractorProvider([extractor]),
                scope.ServiceProvider.GetRequiredService<IEventNotificationService>(),
                NullLogger<ExtractFileManifestMetadataRequestHandler>.Instance);

            await handler.Handle(new ExtractFileManifestMetadataRequest { FileManifestId = original.FileManifestId }, CancellationToken.None);

            Assert.That(extractor.ContentTypes, Is.EquivalentTo(new[] { "image/png", "image/jpeg" }));
            FileManifestMetadataState result = await GetFileManifestMetadataStateAsync(original.Id);
            Assert.That(result.Metadata?[FileContentMetadataKeys.ImageWidth], Is.EqualTo("32"));
        }
    }
}
