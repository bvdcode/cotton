// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using static Cotton.Server.IntegrationTests.Helpers.PreviewFixtures;

namespace Cotton.Server.IntegrationTests
{
    public class MetadataExtractionFailuresTests : PreviewTestBase
    {
        [Test]
        public async Task UnavailableMetadata_RecordsVersionAndError_AndDoesNotRetry()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto healthy = await Pipeline.UploadAndCreateFileAsync(root.Id, "healthy.png", "image/png",
                CreateGradientPngBytes(24, 16));
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "failed.png", "image/png",
                CreateGradientPngBytes(32, 24));
            await Pipeline.UpdateFileManifestAsync(file.Id, manifest => manifest.Metadata = new()
            {
                ["custom.label"] = "Keep",
                ["image.width"] = "old",
            });
            RecordingContentMetadataExtractor extractor = new(["image/png"], (_, _) =>
                throw new FileMetadataUnavailableException("Invalid file metadata."));
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            ExtractFileManifestMetadataRequestHandler handler = CreateHandler(scope, extractor);
            ExtractFileManifestMetadataRequest request = new() { FileManifestId = file.FileManifestId };

            await handler.Handle(request, CancellationToken.None);
            await handler.Handle(request, CancellationToken.None);
            await Pipeline.ExecuteExtractFileMetadataJobAsync();

            Dictionary<string, string> metadata = (await Pipeline.GetFileManifestMetadataStateAsync(file.Id)).Metadata!;
            Assert.Multiple(() =>
            {
                Assert.That(extractor.ContentTypes, Has.Count.EqualTo(1));
                Assert.That(metadata[FileContentMetadataKeys.ExtractionVersion], Is.EqualTo(FileContentMetadataKeys.CurrentExtractionVersion));
                Assert.That(metadata[FileContentMetadataKeys.ExtractionError], Is.EqualTo("Invalid file metadata."));
                Assert.That(metadata["custom.label"], Is.EqualTo("Keep"));
                Assert.That(metadata, Does.Not.ContainKey("image.width"));
            });
            Assert.That((await Pipeline.GetFileManifestMetadataStateAsync(healthy.Id)).Metadata,
                Does.ContainKey(FileContentMetadataKeys.ImageWidth));

            await Pipeline.UpdateFileManifestAsync(file.Id, manifest =>
                manifest.Metadata![FileContentMetadataKeys.ExtractionVersion] = "0");
            await Pipeline.ExecuteExtractFileMetadataJobAsync();
            Dictionary<string, string> retried = (await Pipeline.GetFileManifestMetadataStateAsync(file.Id)).Metadata!;
            Assert.Multiple(() =>
            {
                Assert.That(retried[FileContentMetadataKeys.ImageWidth], Is.EqualTo("32"));
                Assert.That(retried, Does.Not.ContainKey(FileContentMetadataKeys.ExtractionError));
                Assert.That(retried["custom.label"], Is.EqualTo("Keep"));
            });
        }

        [Test]
        public async Task ReadFailure_RetriesExtractionWithoutRecordingCompletedVersion()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "retry.png", "image/png",
                CreateGradientPngBytes(32, 24));
            RecordingContentMetadataExtractor extractor = new(["image/png"], (_, attempt) =>
            {
                if (attempt == 1)
                {
                    throw new IOException("Could not read metadata.");
                }
                return new Dictionary<string, string> { [FileContentMetadataKeys.ImageWidth] = "32" };
            });
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            ExtractFileManifestMetadataRequestHandler handler = CreateHandler(scope, extractor);
            ExtractFileManifestMetadataRequest request = new() { FileManifestId = file.FileManifestId };

            await handler.Handle(request, CancellationToken.None);
            Assert.That((await Pipeline.GetFileManifestMetadataStateAsync(file.Id)).Metadata, Is.Null);
            await handler.Handle(request, CancellationToken.None);

            Dictionary<string, string> metadata = (await Pipeline.GetFileManifestMetadataStateAsync(file.Id)).Metadata!;
            Assert.Multiple(() =>
            {
                Assert.That(extractor.ContentTypes, Has.Count.EqualTo(2));
                Assert.That(metadata[FileContentMetadataKeys.ImageWidth], Is.EqualTo("32"));
                Assert.That(FileContentMetadataDictionary.HasCurrentVersion(metadata), Is.True);
                Assert.That(metadata, Does.Not.ContainKey(FileContentMetadataKeys.ExtractionError));
            });
        }

        [Test]
        public async Task Cancellation_DoesNotRecordCompletedExtraction()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "cancelled.png", "image/png",
                CreateGradientPngBytes(32, 24));
            using CancellationTokenSource cancellation = new();
            RecordingContentMetadataExtractor extractor = new(["image/png"], (_, _) =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            ExtractFileManifestMetadataRequestHandler handler = CreateHandler(scope, extractor);

            Assert.ThrowsAsync<OperationCanceledException>(async () => await handler.Handle(
                new ExtractFileManifestMetadataRequest { FileManifestId = file.FileManifestId }, cancellation.Token));

            Assert.That((await Pipeline.GetFileManifestMetadataStateAsync(file.Id)).Metadata, Is.Null);
        }

        private static ExtractFileManifestMetadataRequestHandler CreateHandler(
            AsyncServiceScope scope, IFileContentMetadataExtractor extractor)
        {
            return new(scope.ServiceProvider.GetRequiredService<CottonDbContext>(),
                scope.ServiceProvider.GetRequiredService<IStoragePipeline>(),
                new FileContentMetadataExtractorProvider([extractor]),
                scope.ServiceProvider.GetRequiredService<IEventNotificationService>(),
                NullLogger<ExtractFileManifestMetadataRequestHandler>.Instance);
        }
    }
}
