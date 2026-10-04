// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Files;
using static Cotton.Server.IntegrationTests.Helpers.PreviewFixtures;

namespace Cotton.Server.IntegrationTests
{
    public class MetadataVersioningTests : PreviewTestBase
    {
        [TestCase(null)]
        [TestCase("0")]
        [TestCase("invalid")]
        public async Task Job_ReextractsOlderMetadataAndPreservesCustomValues(string? oldVersion)
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "photo.png", "image/png",
                CreateGradientPngBytes(32, 24));
            await Pipeline.UpdateFileManifestAsync(file.Id, manifest =>
            {
                manifest.Metadata = new Dictionary<string, string>
                {
                    ["contentMetadata.extractionProcessed"] = "true",
                    ["image.width"] = "999",
                    ["image.removedTag"] = "old value",
                    ["custom.label"] = "Keep this",
                };
                if (oldVersion is not null)
                {
                    manifest.Metadata[FileContentMetadataKeys.ExtractionVersion] = oldVersion;
                }
            });

            await Pipeline.ExecuteExtractFileMetadataJobAsync();

            FileManifestMetadataState state = await Pipeline.GetFileManifestMetadataStateAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(state.Metadata![FileContentMetadataKeys.ExtractionVersion], Is.EqualTo("1"));
                Assert.That(state.Metadata["image.width"], Is.EqualTo("32"));
                Assert.That(state.Metadata["custom.label"], Is.EqualTo("Keep this"));
                Assert.That(state.Metadata, Does.Not.ContainKey("image.removedTag"));
                Assert.That(state.Metadata, Does.Not.ContainKey("contentMetadata.extractionProcessed"));
            });

            Dictionary<string, string> firstResult = new(state.Metadata!);
            await Pipeline.ExecuteExtractFileMetadataJobAsync();
            Assert.That((await Pipeline.GetFileManifestMetadataStateAsync(file.Id)).Metadata, Is.EquivalentTo(firstResult));
        }

        [Test]
        public async Task CurrentVersion_SkipsExtractionInJobAndEndpoint()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "current.png", "image/png",
                CreateGradientPngBytes(32, 24));
            Dictionary<string, string> current = new()
            {
                [FileContentMetadataKeys.ExtractionVersion] = FileContentMetadataKeys.CurrentExtractionVersion,
                ["image.width"] = "777",
            };
            await Pipeline.UpdateFileManifestAsync(file.Id, manifest => manifest.Metadata = current);

            await Pipeline.ExecuteExtractFileMetadataJobAsync();
            using HttpResponseMessage response = await _client!.PostAsync($"/api/v1/files/{file.Id}/metadata/extract", null);
            response.EnsureSuccessStatusCode();
            NodeFileManifestDto returned = (await response.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;

            Assert.That(returned.Metadata, Is.EquivalentTo(current));
            Assert.That((await Pipeline.GetFileManifestMetadataStateAsync(file.Id)).Metadata, Is.EquivalentTo(current));
        }

        [Test]
        public async Task Job_SelectsHeicWithPreviousProcessedFlag()
        {
            Pipeline.SetBearer(await Pipeline.LoginAsync());
            NodeDto root = await Pipeline.GetRootNodeAsync();
            NodeFileManifestDto file = await Pipeline.UploadAndCreateFileAsync(root.Id, "photo.heic", "image/heic",
                CreateGradientPngBytes(32, 24));
            await Pipeline.UpdateFileManifestAsync(file.Id, manifest => manifest.Metadata = new Dictionary<string, string>
            {
                ["contentMetadata.extractionProcessed"] = "true",
            });

            await Pipeline.ExecuteExtractFileMetadataJobAsync();

            FileManifestMetadataState state = await Pipeline.GetFileManifestMetadataStateAsync(file.Id);
            Assert.That(state.Metadata![FileContentMetadataKeys.ExtractionVersion], Is.EqualTo("1"));
            Assert.That(state.Metadata.Keys, Has.Some.StartsWith("image."));
        }
    }
}
