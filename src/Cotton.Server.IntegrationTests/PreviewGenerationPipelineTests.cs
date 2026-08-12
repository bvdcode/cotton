// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Cotton.Server.Services.FileMetadata;
using Cotton.Storage.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class PreviewGenerationPipelineTests : IntegrationTestBase
    {
        private const string PreviewRouteBase = "/api/v1/preview";
        private const string DefaultExternalFixturesDir = @"C:\Temp\cotton-tests";

        private TestAppFactory? _factory;
        private HttpClient? _client;

        private record FixtureUpload(
            Guid NodeFileId,
            string FileName,
            string ContentType,
            int SourceLength,
            bool ExpectLargePreview);

        private record FileManifestPreviewState(
            Guid Id,
            byte[]? SmallFilePreviewHash,
            byte[]? SmallFilePreviewHashEncrypted,
            byte[]? LargeFilePreviewHash,
            string? PreviewGenerationError);

        private record FileManifestMetadataState(
            Dictionary<string, string>? Metadata);

        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();

            Assert.Multiple(() =>
            {
                Assert.That(creator.Exists(), Is.True);
                Assert.That(creator.HasTables(), Is.False);
            });

            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 5432,
                Database = DatabaseName,
                Username = "postgres",
                Password = "postgres"
            };

            var overrides = new Dictionary<string, string?>
            {
                ["DatabaseSettings:Host"] = csb.Host,
                ["DatabaseSettings:Port"] = csb.Port.ToString(),
                ["DatabaseSettings:Database"] = csb.Database,
                ["DatabaseSettings:Username"] = csb.Username,
                ["DatabaseSettings:Password"] = csb.Password,
                ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
                ["MasterEncryptionKeyId"] = "1",
                ["EncryptionThreads"] = "1",
                ["MaxChunkSizeBytes"] = "16777216",
                ["CipherChunkSizeBytes"] = "20971520",
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4"
            };

            _factory = new TestAppFactory(overrides);
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();

            _client = null;
            _factory = null;
        }

        [Test]
        public async Task PreviewPipeline_TextFile_GeneratesSmallPreviewOnly_AndServesCachedWebp()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            byte[] textBytes = Encoding.UTF8.GetBytes("Hello preview pipeline!\nThis is text content for small preview generation.");

            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, "notes.txt", "text/plain", textBytes);

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

            var (smallWidth, smallHeight) = GetImageSize(smallPreview);
            Assert.That(Math.Max(smallWidth, smallHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));

            NodeFileManifestDto listedFile = await GetNodeFileAsync(root.Id, "notes.txt");
            Assert.That(listedFile.PreviewHashEncryptedHex, Is.EqualTo(GetPreviewHashEncryptedHex(manifest.Id, manifest.SmallFilePreviewHashEncrypted)));

            string previewUrl = $"{PreviewRouteBase}/{listedFile.PreviewHashEncryptedHex}";
            HttpResponseMessage previewResponse = await _client!.GetAsync(previewUrl);
            previewResponse.EnsureSuccessStatusCode();

            Assert.That(previewResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
            string? etag = previewResponse.Headers.ETag?.Tag;
            Assert.That(etag, Is.Not.Null.And.Not.Empty);

            byte[] previewBytesFromApi = await previewResponse.Content.ReadAsByteArrayAsync();
            AssertWebpSignature(previewBytesFromApi);

            HttpResponseMessage rawTokenResponse = await _client!.GetAsync($"{PreviewRouteBase}/{Convert.ToHexStringLower(manifest.SmallFilePreviewHashEncrypted!)}");
            Assert.That(rawTokenResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            using HttpRequestMessage conditional = new(HttpMethod.Get, previewUrl);
            conditional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag!));

            using HttpResponseMessage strongNotModified = await _client.SendAsync(conditional);
            Assert.That(strongNotModified.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));

            using HttpRequestMessage weakConditional = new(HttpMethod.Get, previewUrl);
            weakConditional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag!, isWeak: true));

            using HttpResponseMessage weakNotModified = await _client.SendAsync(weakConditional);
            Assert.That(weakNotModified.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));

            using HttpRequestMessage anyConditional = new(HttpMethod.Get, previewUrl);
            anyConditional.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);

            using HttpResponseMessage anyNotModified = await _client.SendAsync(anyConditional);
            Assert.That(anyNotModified.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
        }

        [Test]
        public async Task PreviewPipeline_LargeImage_GeneratesSmallAndLarge_WithExpectedDimensions_AndCompression()
        {
            string token = await LoginAsync();
            SetBearer(token);

            NodeDto root = await GetRootNodeAsync();
            byte[] sourceImage = CreateGradientPngBytes(width: 2200, height: 1200);

            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, "photo.png", "image/png", sourceImage);

            await ExecuteGeneratePreviewJobAsync();

            FileManifestPreviewState manifest = await GetFileManifestByNodeFileIdAsync(createdFile.Id);

            Assert.Multiple(() =>
            {
                Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
                Assert.That(manifest.SmallFilePreviewHashEncrypted, Is.Not.Null);
                Assert.That(manifest.LargeFilePreviewHash, Is.Not.Null);
                Assert.That(manifest.PreviewGenerationError, Is.Null);
            });

            byte[] smallPreview = await ReadPreviewBlobAsync(manifest.SmallFilePreviewHash!);
            byte[] largePreview = await ReadPreviewBlobAsync(manifest.LargeFilePreviewHash!);

            Assert.Multiple(() =>
            {
                AssertWebpSignature(smallPreview);
                AssertWebpSignature(largePreview);
                Assert.That(smallPreview.Length, Is.GreaterThan(0));
                Assert.That(largePreview.Length, Is.GreaterThan(0));
            });

            var (smallWidth, smallHeight) = GetImageSize(smallPreview);
            var (largeWidth, largeHeight) = GetImageSize(largePreview);

            Assert.Multiple(() =>
            {
                Assert.That(Math.Max(smallWidth, smallHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));
                Assert.That(Math.Max(largeWidth, largeHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultLargePreviewSize));
                Assert.That((largeWidth * largeHeight), Is.GreaterThan(smallWidth * smallHeight));
            });

            Chunk smallChunk = await GetChunkByHashAsync(manifest.SmallFilePreviewHash!);
            Chunk largeChunk = await GetChunkByHashAsync(manifest.LargeFilePreviewHash!);

            Assert.Multiple(() =>
            {
                Assert.That(smallChunk.PlainSizeBytes, Is.EqualTo(smallPreview.Length));
                Assert.That(smallChunk.StoredSizeBytes, Is.GreaterThan(0));
                Assert.That(largeChunk.PlainSizeBytes, Is.EqualTo(largePreview.Length));
                Assert.That(largeChunk.StoredSizeBytes, Is.GreaterThan(0));
            });
        }

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

            await AddMetadataPersistenceFailureConstraintAsync();
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
                await RemoveMetadataPersistenceFailureConstraintAsync();
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

        private async Task ExecuteGeneratePreviewJobAsync()
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            GeneratePreviewJob job = ActivatorUtilities.CreateInstance<GeneratePreviewJob>(scope.ServiceProvider);
            await job.Execute(null!);
        }

        private async Task ExecuteExtractFileMetadataJobAsync()
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            ExtractFileMetadataJob job = ActivatorUtilities.CreateInstance<ExtractFileMetadataJob>(scope.ServiceProvider);
            await job.Execute(null!);
        }

        private async Task UpdateFileManifestAsync(Guid nodeFileId, Action<FileManifest> update)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await dbContext.NodeFiles
                .Where(x => x.Id == nodeFileId)
                .Select(x => x.FileManifest)
                .SingleAsync();

            update(manifest);
            await dbContext.SaveChangesAsync();
        }

        private async Task<FileManifestMetadataState> GetFileManifestMetadataStateAsync(Guid nodeFileId)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            return await dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId)
                .Select(x => new FileManifestMetadataState(
                    x.FileManifest.Metadata))
                .SingleAsync();
        }

        private async Task AddMetadataPersistenceFailureConstraintAsync()
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE file_manifests ADD CONSTRAINT ck_file_manifests_metadata_persistence_test CHECK (metadata IS NULL)");
        }

        private async Task RemoveMetadataPersistenceFailureConstraintAsync()
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE file_manifests DROP CONSTRAINT IF EXISTS ck_file_manifests_metadata_persistence_test");
        }

        private async Task<Chunk> GetChunkByHashAsync(byte[] hash)
        {
            Chunk? chunk = await DbContext.Chunks.FindAsync([hash]);
            Assert.That(chunk, Is.Not.Null, "Preview chunk row is missing in DB.");
            return chunk!;
        }

        private static async Task<FileManifest> LoadFileManifestAsync(
            CottonDbContext dbContext,
            Guid nodeFileId)
        {
            return await dbContext.NodeFiles
                .Where(x => x.Id == nodeFileId)
                .Select(x => x.FileManifest)
                .SingleAsync();
        }

        private async Task<FileManifestPreviewState> GetFileManifestByNodeFileIdAsync(Guid nodeFileId)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            FileManifestPreviewState? manifest = await dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId)
                .Select(x => new FileManifestPreviewState(
                    x.FileManifest.Id,
                    x.FileManifest.SmallFilePreviewHash,
                    x.FileManifest.SmallFilePreviewHashEncrypted,
                    x.FileManifest.LargeFilePreviewHash,
                    x.FileManifest.PreviewGenerationError))
                .SingleOrDefaultAsync();

            Assert.That(manifest, Is.Not.Null);
            return manifest!;
        }

        private static string? GetPreviewHashEncryptedHex(Guid manifestId, byte[]? encryptedHash)
        {
            return encryptedHash is null
                ? null
                : string.Concat(FileManifest.PreviewTokenPrefix, manifestId.ToString("N"), Convert.ToHexStringLower(encryptedHash));
        }

        private async Task<byte[]> ReadPreviewBlobAsync(byte[] hash)
        {
            string storageKey = Hasher.ToHexStringHash(hash);

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            IStoragePipeline storage = scope.ServiceProvider.GetRequiredService<IStoragePipeline>();

            await using Stream stream = await storage.ReadAsync(storageKey);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }

        private async Task<NodeFileManifestDto> UploadAndCreateFileAsync(Guid nodeId, string fileName, string contentType, byte[] content)
        {
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));

            using var uploadForm = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers =
                        {
                            ContentType = new MediaTypeHeaderValue("application/octet-stream")
                        }
                    },
                    "file",
                    fileName
                },
                {
                    new StringContent(chunkHashLower),
                    "hash"
                }
            };

            HttpResponseMessage uploadResponse = await _client!.PostAsync("/api/v1/chunks", uploadForm);
            uploadResponse.EnsureSuccessStatusCode();

            var createFileRequest = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [chunkHashLower],
                Name = fileName,
                ContentType = contentType,
                Hash = chunkHashLower,
                NodeId = nodeId,
            };

            HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", createFileRequest);
            createResponse.EnsureSuccessStatusCode();

            return await GetNodeFileAsync(nodeId, fileName);
        }

        private async Task<NodeFileManifestDto> GetNodeFileAsync(Guid nodeId, string fileName)
        {
            NodeContentDto? content = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
            Assert.That(content, Is.Not.Null);

            NodeFileManifestDto? file = content!.Files.SingleOrDefault(x => x.Name == fileName);
            Assert.That(file, Is.Not.Null, $"Node file '{fileName}' was not found in node {nodeId}.");
            return file!;
        }

        private async Task<NodeDto> GetRootNodeAsync()
        {
            NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            return root!;
        }

        private async Task<string> LoginAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new LoginRequestDto
                {
                    Username = "testuser",
                    Password = "testpassword"
                })
            };

            request.Headers.Add("X-Forwarded-For", "8.8.8.8");

            HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            TokenPairResponseDto? payload = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(payload, Is.Not.Null);

            return payload!.AccessToken;
        }

        private void SetBearer(string accessToken)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        private static (int Width, int Height) GetImageSize(byte[] imageBytes)
        {
            ImageInfo info = Image.Identify(imageBytes);
            Assert.That(info, Is.Not.Null, "Failed to identify preview image format and dimensions.");
            return (info!.Width, info.Height);
        }

        private static void AssertWebpSignature(byte[] imageBytes)
        {
            Assert.That(imageBytes.Length, Is.GreaterThanOrEqualTo(12));
            Assert.That(Encoding.ASCII.GetString(imageBytes, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(Encoding.ASCII.GetString(imageBytes, 8, 4), Is.EqualTo("WEBP"));
        }

        private static bool ExpectsLargePreview(string contentType)
        {
            return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ResolveContentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".xml" => "application/xml",
                ".json" => "application/json",
                ".js" => "application/javascript",
                ".pdf" => "application/pdf",
                ".stl" => "model/stl",
                ".obj" => "model/obj",
                ".3mf" => "model/3mf",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".tif" => "image/tiff",
                ".tiff" => "image/tiff",
                ".heic" => "image/heic",
                ".heif" => "image/heif",
                ".mp3" => "audio/mpeg",
                ".flac" => "audio/flac",
                ".wav" => "audio/wav",
                ".aac" => "audio/aac",
                ".m4a" => "audio/x-m4a",
                ".ogg" => "audio/ogg",
                ".opus" => "audio/opus",
                ".aiff" => "audio/aiff",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/mov",
                ".mkv" => "video/mkv",
                ".avi" => "video/avi",
                _ => null
            };
        }

        private static string ResolveExternalFixturesDir()
        {
            string? fromEnvironment = Environment.GetEnvironmentVariable("COTTON_PREVIEW_FIXTURES_DIR");
            return string.IsNullOrWhiteSpace(fromEnvironment)
                ? DefaultExternalFixturesDir
                : fromEnvironment;
        }

        private static void EnsureDefaultFixturesExist(string fixturesDir)
        {
            if (Directory.EnumerateFiles(fixturesDir).Any())
            {
                return;
            }

            File.WriteAllText(
                Path.Combine(fixturesDir, "01_text.txt"),
                "Cotton preview fixture: plain text file for generator coverage.");

            File.WriteAllText(
                Path.Combine(fixturesDir, "02_markdown.md"),
                "# Cotton Preview Fixture\n\nThis file validates markdown preview rendering.");

            File.WriteAllText(
                Path.Combine(fixturesDir, "03_data.json"),
                "{\"name\":\"cotton\",\"kind\":\"preview-fixture\"}");

            File.WriteAllText(
                Path.Combine(fixturesDir, "04_data.xml"),
                "<root><name>cotton</name><kind>preview-fixture</kind></root>");

            File.WriteAllBytes(
                Path.Combine(fixturesDir, "05_image.png"),
                CreateGradientPngBytes(width: 1600, height: 900));

            File.WriteAllBytes(
                Path.Combine(fixturesDir, "06_document.pdf"),
                CreateSinglePagePdfBytes("Cotton preview fixture PDF"));
        }

        private static byte[] CreateGradientPngBytes(int width, int height)
        {
            using var image = new Image<Rgba32>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte red = (byte)((x * 255) / Math.Max(1, width - 1));
                    byte green = (byte)((y * 255) / Math.Max(1, height - 1));
                    byte blue = (byte)((x + y) % 256);
                    image[x, y] = new Rgba32(red, green, blue, 255);
                }
            }

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        private static byte[] CreateTruncatedPngBytes() =>
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01,
        ];

        private static async Task<byte[]> CreateAudioBytesAsync(
            string? title = null,
            string? artist = null,
            string? album = null)
        {
            await FfmpegBinary.EnsureAvailableAsync();
            ProcessStartInfo startInfo = new()
            {
                FileName = FfmpegBinary.GetFfmpegPath(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            string[] commonArguments =
            [
                "-hide_banner",
                "-loglevel",
                "error",
                "-f",
                "lavfi",
                "-i",
                "anullsrc=r=8000:cl=mono",
                "-t",
                "0.1"
            ];
            foreach (string argument in commonArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            AddMetadataArgument(startInfo, "title", title);
            AddMetadataArgument(startInfo, "artist", artist);
            AddMetadataArgument(startInfo, "album", album);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("mp3");
            startInfo.ArgumentList.Add("pipe:1");

            using Process process = new() { StartInfo = startInfo };
            Assert.That(process.Start(), Is.True);
            using MemoryStream output = new();
            Task copyTask = process.StandardOutput.BaseStream.CopyToAsync(output);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(copyTask, process.WaitForExitAsync());
            string stderr = await stderrTask;
            Assert.That(process.ExitCode, Is.EqualTo(0), stderr);
            return output.ToArray();
        }

        private static void AddMetadataArgument(
            ProcessStartInfo startInfo,
            string key,
            string? value)
        {
            if (value is null)
            {
                return;
            }

            startInfo.ArgumentList.Add("-metadata");
            startInfo.ArgumentList.Add($"{key}={value}");
        }

        private static byte[] CreateSinglePagePdfBytes(string text)
        {
            string escaped = text
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);

            string content = $"BT /F1 24 Tf 50 140 Td ({escaped}) Tj ET";
            byte[] contentBytes = Encoding.ASCII.GetBytes(content);

            string[] objects =
            [
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
                $"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
            ];

            using var ms = new MemoryStream();
            var offsets = new List<long> { 0 };

            static void WriteAscii(MemoryStream stream, string value)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length);
            }

            WriteAscii(ms, "%PDF-1.4\n");

            for (int i = 0; i < objects.Length; i++)
            {
                offsets.Add(ms.Position);
                WriteAscii(ms, $"{i + 1} 0 obj\n");
                WriteAscii(ms, objects[i]);
                WriteAscii(ms, "\nendobj\n");
            }

            long xrefOffset = ms.Position;

            WriteAscii(ms, $"xref\n0 {offsets.Count}\n");
            WriteAscii(ms, "0000000000 65535 f \n");
            for (int i = 1; i < offsets.Count; i++)
            {
                WriteAscii(ms, $"{offsets[i]:0000000000} 00000 n \n");
            }

            WriteAscii(ms, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
            return ms.ToArray();
        }
    }
}
