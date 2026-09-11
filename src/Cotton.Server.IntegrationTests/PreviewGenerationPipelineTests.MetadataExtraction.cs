// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [Test]
        public async Task MetadataExtraction_ImageFile_StoresManifestMetadataAndReturnsMergedDto()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            byte[] sourceImage = CreateGradientPngBytes(width: 320, height: 240);

            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, "photo.png", "image/png", sourceImage);

            HttpResponseMessage response = await _client!.PostAsync($"/api/v1/files/{createdFile.Id}/metadata/extract", null);
            response.EnsureSuccessStatusCode();

            NodeFileManifestDto? extractedFile = await response.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(extractedFile, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(extractedFile!.Metadata["image.width"], Is.EqualTo("320"));
                Assert.That(extractedFile.Metadata["image.height"], Is.EqualTo("240"));
            });

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == createdFile.Id)
                .Select(x => x.FileManifest)
                .SingleAsync();

            Assert.Multiple(() =>
            {
                Assert.That(manifest.Metadata?["image.width"], Is.EqualTo("320"));
                Assert.That(manifest.Metadata?["image.height"], Is.EqualTo("240"));
            });
        }

        [Test]
        public async Task MetadataExtraction_CorruptRecognizedImage_MarksAttemptProcessed()
        {
            byte[] corruptImage = CreateTruncatedPngBytes();
            InvalidImageContentException? invalidContent = Assert.ThrowsAsync<InvalidImageContentException>(async () =>
            {
                await using MemoryStream stream = new(corruptImage, writable: false);
                await Image.IdentifyAsync(stream);
            });
            Assert.That(invalidContent, Is.Not.Null, "The fixture must remain a recognized PNG with invalid content.");

            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(
                root.Id,
                "corrupt.png",
                "image/png",
                corruptImage);

            await ExecuteExtractFileMetadataJobAsync();

            FileManifestMetadataState processedState = await GetFileManifestMetadataStateAsync(createdFile.Id);
            Assert.That(processedState.Metadata, Is.Not.Null);
            Dictionary<string, string> processedMetadata = processedState.Metadata!;
            Assert.Multiple(() =>
            {
                Assert.That(processedMetadata, Does.ContainKey(FileContentMetadataKeys.ExtractionProcessed));
                Assert.That(processedMetadata, Does.Not.ContainKey(FileContentMetadataKeys.ImageWidth));
                Assert.That(processedMetadata, Does.Not.ContainKey(FileContentMetadataKeys.ImageHeight));
            });

            await ExecuteExtractFileMetadataJobAsync();
            FileManifestMetadataState repeatedState = await GetFileManifestMetadataStateAsync(createdFile.Id);
            Assert.That(repeatedState.Metadata, Is.EquivalentTo(processedMetadata));
        }

        [Test]
        public async Task MetadataExtraction_PersistenceFailure_ReturnsServerErrorWithoutPhantomMetadata()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(
                root.Id,
                "persistence-failure.png",
                "image/png",
                CreateGradientPngBytes(width: 32, height: 24));

            _metadataFailure.Enabled = true;
            try
            {
                HttpResponseMessage response = await _client!.PostAsync(
                    $"/api/v1/files/{createdFile.Id}/metadata/extract",
                    null);

                FileManifestMetadataState persistedState = await GetFileManifestMetadataStateAsync(createdFile.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
                    Assert.That(persistedState.Metadata, Is.Null);
                });
            }
            finally
            {
                _metadataFailure.Enabled = false;
            }
        }

        [Test]
        public async Task DatabaseIntegrity_ConcurrentSignedManifestWrites_RejectStaleSave()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            byte[] content = Encoding.UTF8.GetBytes("concurrent integrity test");
            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(
                root.Id,
                "concurrent.txt",
                "text/plain",
                content);

            await using AsyncServiceScope previewScope = _factory!.Services.CreateAsyncScope();
            await using AsyncServiceScope hashScope = _factory.Services.CreateAsyncScope();
            CottonDbContext previewContext = previewScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            CottonDbContext hashContext = hashScope.ServiceProvider.GetRequiredService<CottonDbContext>();

            FileManifest previewManifest = await LoadFileManifestAsync(previewContext, createdFile.Id);
            FileManifest hashManifest = await LoadFileManifestAsync(hashContext, createdFile.Id);
            byte[]? originalComputedHash = hashManifest.ComputedContentHash?.ToArray();

            byte[] previewHash = Hasher.HashData(Encoding.UTF8.GetBytes("preview"));
            byte[] computedHash = Hasher.HashData(Encoding.UTF8.GetBytes("computed"));
            previewManifest.SmallFilePreviewHash = previewHash;
            await previewContext.SaveChangesAsync();

            hashManifest.ComputedContentHash = computedHash;
            DbUpdateConcurrencyException? conflict = Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                async () => await hashContext.SaveChangesAsync());
            Assert.That(conflict, Is.Not.Null);

            await using AsyncServiceScope verificationScope = _factory.Services.CreateAsyncScope();
            CottonDbContext verificationContext = verificationScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest persistedManifest = await LoadFileManifestAsync(verificationContext, createdFile.Id);
            IDatabaseIntegrityProtector protector = verificationScope.ServiceProvider.GetRequiredService<IDatabaseIntegrityProtector>();
            byte[]? persistedMac = verificationContext.Entry(persistedManifest)
                .Property<byte[]?>(DatabaseIntegrityColumns.MacProperty)
                .CurrentValue;

            Assert.Multiple(() =>
            {
                Assert.That(persistedManifest.SmallFilePreviewHash, Is.EqualTo(previewHash));
                Assert.That(persistedManifest.ComputedContentHash, Is.EqualTo(originalComputedHash));
                Assert.That(persistedMac, Is.Not.Null);
                Assert.That(
                    protector.Verify(persistedManifest, new FileManifestIntegrityDescriptor(), persistedMac!),
                    Is.True);
            });
        }

        [Test]
        public async Task MetadataExtraction_TaggedAudio_StoresTitleArtistAndAlbum()
        {
            const string title = "Pipeline title";
            const string artist = "Pipeline artist";
            const string album = "Pipeline album";
            string token = await LoginAsync();
            SetBearer(token);
            NodeDto root = await GetRootNodeAsync();
            byte[] audio = await CreateAudioBytesAsync(title, artist, album);
            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(
                root.Id,
                "tagged.mp3",
                "audio/mpeg",
                audio);

            using HttpResponseMessage response = await _client!.PostAsync(
                $"/api/v1/files/{createdFile.Id}/metadata/extract",
                null);
            response.EnsureSuccessStatusCode();

            NodeFileManifestDto? extractedFile = await response.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(extractedFile, Is.Not.Null);
            FileManifestMetadataState persisted = await GetFileManifestMetadataStateAsync(createdFile.Id);
            Assert.Multiple(() =>
            {
                Assert.That(extractedFile!.Metadata[FileContentMetadataKeys.MediaTitle], Is.EqualTo(title));
                Assert.That(extractedFile.Metadata[FileContentMetadataKeys.MediaArtist], Is.EqualTo(artist));
                Assert.That(extractedFile.Metadata[FileContentMetadataKeys.MediaAlbum], Is.EqualTo(album));
                Assert.That(persisted.Metadata?[FileContentMetadataKeys.MediaTitle], Is.EqualTo(title));
                Assert.That(persisted.Metadata?[FileContentMetadataKeys.MediaArtist], Is.EqualTo(artist));
                Assert.That(persisted.Metadata?[FileContentMetadataKeys.MediaAlbum], Is.EqualTo(album));
            });
        }

        [Test]
        public async Task MetadataExtraction_ValidAudioWithoutTags_IsNotMarkedFailed()
        {
            string token = await LoginAsync();
            SetBearer(token);
            NodeDto root = await GetRootNodeAsync();
            byte[] audio = await CreateAudioBytesAsync();
            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(
                root.Id,
                "untagged.mp3",
                "audio/mpeg",
                audio);

            using HttpResponseMessage response = await _client!.PostAsync(
                $"/api/v1/files/{createdFile.Id}/metadata/extract",
                null);
            response.EnsureSuccessStatusCode();

            NodeFileManifestDto? extractedFile = await response.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(extractedFile, Is.Not.Null);
            FileManifestMetadataState persisted = await GetFileManifestMetadataStateAsync(createdFile.Id);
            Assert.Multiple(() =>
            {
                Assert.That(extractedFile!.Metadata, Does.ContainKey(FileContentMetadataKeys.MediaAudioCodec));
                Assert.That(extractedFile.Metadata, Does.ContainKey(FileContentMetadataKeys.MediaDurationSeconds));
                Assert.That(extractedFile.Metadata, Does.Not.ContainKey(FileContentMetadataKeys.MediaTitle));
                Assert.That(persisted.Metadata, Does.ContainKey(FileContentMetadataKeys.MediaAudioCodec));
                Assert.That(persisted.Metadata, Does.ContainKey(FileContentMetadataKeys.MediaDurationSeconds));
            });
        }

    }
}
