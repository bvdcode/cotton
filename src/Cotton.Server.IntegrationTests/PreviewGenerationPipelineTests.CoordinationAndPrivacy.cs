// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
        [Test]
        public async Task PreviewAndMetadataJobs_SameManifest_PersistBothResultsWithValidIntegrityMac()
        {
            string token = await LoginAsync();
            SetBearer(token);
            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(
                root.Id,
                "combined.png",
                "image/png",
                CreateGradientPngBytes(width: 96, height: 64));

            await ExecuteGeneratePreviewJobAsync();
            await ExecuteExtractFileMetadataJobAsync();

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await LoadFileManifestAsync(dbContext, createdFile.Id);
            byte[]? integrityMac = dbContext.Entry(manifest)
                .Property<byte[]?>(DatabaseIntegrityColumns.MacProperty)
                .CurrentValue;
            IDatabaseIntegrityProtector protector =
                scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityProtector>();
            Assert.Multiple(() =>
            {
                Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
                Assert.That(manifest.SmallFilePreviewHashEncrypted, Is.Not.Null);
                Assert.That(manifest.PreviewGenerationError, Is.Null);
                Assert.That(manifest.Metadata?[FileContentMetadataKeys.ImageWidth], Is.EqualTo("96"));
                Assert.That(manifest.Metadata?[FileContentMetadataKeys.ImageHeight], Is.EqualTo("64"));
                Assert.That(integrityMac, Is.Not.Null);
                Assert.That(
                    protector.Verify(manifest, new FileManifestIntegrityDescriptor(), integrityMac!),
                    Is.True);
            });
        }

        [Test]
        public async Task MetadataExtraction_DoesNotLogRawTagsPathsOrRecipientLikeValues()
        {
            const string pathLikeTitle = @"<local root>\music\track.mp3";
            const string recipientLikeArtist = "<account>@example.invalid";
            const string album = "<server profile> album";
            byte[] audio = await CreateAudioBytesAsync(
                pathLikeTitle,
                recipientLikeArtist,
                album);
            NUnitLoggerProvider loggerProvider = new();
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                builder.AddProvider(loggerProvider));
            MediaFileContentMetadataExtractor extractor = new(
                loggerFactory.CreateLogger<MediaFileContentMetadataExtractor>());
            await using MemoryStream stream = new(audio, writable: false);

            IReadOnlyDictionary<string, string> metadata = await extractor.ExtractAsync(
                stream,
                "audio/mpeg",
                CancellationToken.None);

            string logs = string.Join("\n", loggerProvider.Messages);
            Assert.Multiple(() =>
            {
                Assert.That(metadata[FileContentMetadataKeys.MediaTitle], Is.EqualTo(pathLikeTitle));
                Assert.That(metadata[FileContentMetadataKeys.MediaArtist], Is.EqualTo(recipientLikeArtist));
                Assert.That(logs, Does.Not.Contain(pathLikeTitle));
                Assert.That(logs, Does.Not.Contain(recipientLikeArtist));
                Assert.That(logs, Does.Not.Contain(album));
            });
        }

        [Test]
        public async Task MetadataExtraction_InvalidMedia_PreservesCustomMetadataAndMarksEmptyAttemptsProcessed()
        {
            const string ExistingTitle = "Existing title";
            const string ExistingKey = "custom.title";

            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            NodeFileManifestDto validImage = await UploadAndCreateFileAsync(
                root.Id,
                "valid.png",
                "image/png",
                CreateGradientPngBytes(width: 64, height: 48));
            byte[] invalidAudio = Encoding.UTF8.GetBytes("This is not a valid audio file.");
            byte[] otherInvalidAudio = Encoding.UTF8.GetBytes("This is not a valid audio file either.");
            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(
                root.Id,
                "invalid.mp3",
                "audio/mpeg",
                invalidAudio);
            NodeFileManifestDto emptyMetadataFile = await UploadAndCreateFileAsync(
                root.Id,
                "invalid-empty.mp3",
                "audio/mpeg",
                otherInvalidAudio);

            await UpdateFileManifestAsync(createdFile.Id, manifest =>
            {
                manifest.Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ExistingKey] = ExistingTitle,
                };
            });

            HttpResponseMessage firstAttempt = await _client!.PostAsync(
                $"/api/v1/files/{createdFile.Id}/metadata/extract",
                null);
            firstAttempt.EnsureSuccessStatusCode();

            FileManifestMetadataState failedState = await GetFileManifestMetadataStateAsync(createdFile.Id);
            Assert.That(failedState.Metadata?[ExistingKey], Is.EqualTo(ExistingTitle));

            HttpResponseMessage emptyAttempt = await _client!.PostAsync(
                $"/api/v1/files/{emptyMetadataFile.Id}/metadata/extract",
                null);
            emptyAttempt.EnsureSuccessStatusCode();
            NodeFileManifestDto? emptyAttemptDto = await emptyAttempt.Content.ReadFromJsonAsync<NodeFileManifestDto>();

            FileManifestMetadataState emptyFailedState = await GetFileManifestMetadataStateAsync(emptyMetadataFile.Id);
            Assert.Multiple(() =>
            {
                Assert.That(emptyFailedState.Metadata, Is.Not.Null);
                Assert.That(emptyFailedState.Metadata, Does.ContainKey(FileContentMetadataKeys.ExtractionProcessed));
                Assert.That(emptyAttemptDto?.Metadata, Is.Not.Null);
                Assert.That(emptyAttemptDto!.Metadata, Does.Not.ContainKey(FileContentMetadataKeys.ExtractionProcessed));
            });

            await ExecuteExtractFileMetadataJobAsync();

            FileManifestMetadataState invalidMediaState = await GetFileManifestMetadataStateAsync(createdFile.Id);
            FileManifestMetadataState emptyInvalidMediaState = await GetFileManifestMetadataStateAsync(emptyMetadataFile.Id);
            FileManifestMetadataState validImageState = await GetFileManifestMetadataStateAsync(validImage.Id);
            Assert.Multiple(() =>
            {
                Assert.That(invalidMediaState.Metadata?[ExistingKey], Is.EqualTo(ExistingTitle));
                Assert.That(invalidMediaState.Metadata, Does.ContainKey(FileContentMetadataKeys.ExtractionProcessed));
                Assert.That(emptyInvalidMediaState.Metadata, Is.Not.Null);
                Assert.That(emptyInvalidMediaState.Metadata, Does.ContainKey(FileContentMetadataKeys.ExtractionProcessed));
                Assert.That(validImageState.Metadata?[FileContentMetadataKeys.ImageWidth], Is.EqualTo("64"));
                Assert.That(validImageState.Metadata?[FileContentMetadataKeys.ImageHeight], Is.EqualTo("48"));
            });
        }

    }
}
