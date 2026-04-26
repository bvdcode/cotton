// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Cotton.Server.IntegrationTests;

public class PreviewGenerationPipelineTests : IntegrationTestBase
{
    private const string PreviewRouteBase = "/api/v1/previews";

    private TestAppFactory? _factory;
    private HttpClient? _client;

    private sealed record FixtureUpload(
        Guid NodeFileId,
        string FileName,
        string ContentType,
        int SourceLength,
        bool ExpectLargePreview);

    [SetUp]
    public void SetUp()
    {
        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
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

        FileManifest manifest = await GetFileManifestByNodeFileIdAsync(createdFile.Id);

        Assert.Multiple(() =>
        {
            Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
            Assert.That(manifest.SmallFilePreviewHashEncrypted, Is.Not.Null);
            Assert.That(manifest.LargeFilePreviewHash, Is.Null);
            Assert.That(manifest.PreviewGenerationError, Is.Null);
        });

        byte[] smallPreview = await ReadPreviewBlobAsync(manifest.SmallFilePreviewHash!);
        AssertWebpSignature(smallPreview);

        (int smallWidth, int smallHeight) = GetImageSize(smallPreview);
        Assert.That(Math.Max(smallWidth, smallHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));

        NodeFileManifestDto listedFile = await GetNodeFileAsync(root.Id, "notes.txt");
        Assert.That(listedFile.PreviewHashEncryptedHex, Is.EqualTo(manifest.GetPreviewHashEncryptedHex()));

        HttpResponseMessage previewResponse = await _client!.GetAsync($"{PreviewRouteBase}/{listedFile.PreviewHashEncryptedHex}");
        previewResponse.EnsureSuccessStatusCode();

        Assert.That(previewResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
        string? etag = previewResponse.Headers.ETag?.Tag;
        Assert.That(etag, Is.Not.Null.And.Not.Empty);

        byte[] previewBytesFromApi = await previewResponse.Content.ReadAsByteArrayAsync();
        AssertWebpSignature(previewBytesFromApi);

        using var conditional = new HttpRequestMessage(HttpMethod.Get, $"{PreviewRouteBase}/{listedFile.PreviewHashEncryptedHex}");
        conditional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag!));

        HttpResponseMessage notModified = await _client.SendAsync(conditional);
        Assert.That(notModified.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
    }

    [Test]
    public async Task PreviewPipeline_LargeImage_GeneratesSmallAndLarge_WithExpectedDimensions_AndCompression()
    {
        string token = await LoginAsync();
        SetBearer(token);

        NodeDto root = await GetRootNodeAsync();
        byte[] sourceImage = CreateGradientBitmapBytes(width: 2200, height: 1200);

        NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, "photo.bmp", "image/bmp", sourceImage);

        await ExecuteGeneratePreviewJobAsync();

        FileManifest manifest = await GetFileManifestByNodeFileIdAsync(createdFile.Id);

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
            Assert.That(smallPreview.Length, Is.LessThan(sourceImage.Length));
            Assert.That(largePreview.Length, Is.LessThan(sourceImage.Length));
        });

        (int smallWidth, int smallHeight) = GetImageSize(smallPreview);
        (int largeWidth, int largeHeight) = GetImageSize(largePreview);

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
    public async Task PreviewPipeline_PdfFile_GeneratesSmallPreviewOnly_AndReturnsWebpFromEndpoint()
    {
        string token = await LoginAsync();
        SetBearer(token);

        NodeDto root = await GetRootNodeAsync();
        byte[] pdfBytes = CreateSinglePagePdfBytes("Preview PDF E2E");

        NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, "document.pdf", "application/pdf", pdfBytes);

        await ExecuteGeneratePreviewJobAsync();

        FileManifest manifest = await GetFileManifestByNodeFileIdAsync(createdFile.Id);

        Assert.Multiple(() =>
        {
            Assert.That(manifest.SmallFilePreviewHash, Is.Not.Null);
            Assert.That(manifest.SmallFilePreviewHashEncrypted, Is.Not.Null);
            Assert.That(manifest.LargeFilePreviewHash, Is.Null);
            Assert.That(manifest.PreviewGenerationError, Is.Null);
        });

        byte[] smallPreview = await ReadPreviewBlobAsync(manifest.SmallFilePreviewHash!);
        AssertWebpSignature(smallPreview);

        (int width, int height) = GetImageSize(smallPreview);
        Assert.That(Math.Max(width, height), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));

        HttpResponseMessage response = await _client!.GetAsync($"{PreviewRouteBase}/{manifest.GetPreviewHashEncryptedHex()}");
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

        FileManifest manifest = await GetFileManifestByNodeFileIdAsync(createdFile.Id);
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
        string? fixturesDir = Environment.GetEnvironmentVariable("COTTON_PREVIEW_FIXTURES_DIR");
        if (string.IsNullOrWhiteSpace(fixturesDir) || !Directory.Exists(fixturesDir))
        {
            Assert.Ignore("Set COTTON_PREVIEW_FIXTURES_DIR to run external preview fixture coverage.");
        }

        string[] files = Directory
            .GetFiles(fixturesDir)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            Assert.Ignore("No fixtures found in COTTON_PREVIEW_FIXTURES_DIR.");
        }

        string token = await LoginAsync();
        SetBearer(token);

        NodeDto root = await GetRootNodeAsync();
        List<FixtureUpload> uploads = [];

        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            string? contentType = ResolveContentType(filePath);
            Assert.That(contentType, Is.Not.Null, $"Unsupported fixture extension for {fileName}.");

            byte[] source = await File.ReadAllBytesAsync(filePath);
            NodeFileManifestDto createdFile = await UploadAndCreateFileAsync(root.Id, fileName, contentType!, source);

            uploads.Add(new FixtureUpload(
                NodeFileId: createdFile.Id,
                FileName: fileName,
                ContentType: contentType!,
                SourceLength: source.Length,
                ExpectLargePreview: ExpectsLargePreview(contentType!)));
        }

        await ExecuteGeneratePreviewJobAsync();

        foreach (FixtureUpload upload in uploads)
        {
            FileManifest manifest = await GetFileManifestByNodeFileIdAsync(upload.NodeFileId);

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
            (int smallWidth, int smallHeight) = GetImageSize(smallPreview);
            Assert.That(Math.Max(smallWidth, smallHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultSmallPreviewSize));

            if (manifest.LargeFilePreviewHash is not null)
            {
                byte[] largePreview = await ReadPreviewBlobAsync(manifest.LargeFilePreviewHash);
                AssertWebpSignature(largePreview);

                (int largeWidth, int largeHeight) = GetImageSize(largePreview);
                Assert.That(Math.Max(largeWidth, largeHeight), Is.LessThanOrEqualTo(PreviewGeneratorProvider.DefaultLargePreviewSize));
                Assert.That(largeWidth * largeHeight, Is.GreaterThanOrEqualTo(smallWidth * smallHeight));

                if (upload.SourceLength > 0)
                {
                    Assert.That(largePreview.Length, Is.LessThan(upload.SourceLength));
                }
            }

            NodeFileManifestDto listedFile = await GetNodeFileAsync(root.Id, upload.FileName);
            Assert.That(listedFile.PreviewHashEncryptedHex, Is.EqualTo(manifest.GetPreviewHashEncryptedHex()));

            HttpResponseMessage response = await _client!.GetAsync($"{PreviewRouteBase}/{listedFile.PreviewHashEncryptedHex}");
            response.EnsureSuccessStatusCode();
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
        }
    }

    private async Task ExecuteGeneratePreviewJobAsync()
    {
        await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
        GeneratePreviewJob job = scope.ServiceProvider.GetRequiredService<GeneratePreviewJob>();
        await job.Execute(null!);
    }

    private async Task<Chunk> GetChunkByHashAsync(byte[] hash)
    {
        Chunk? chunk = await DbContext.Chunks.FindAsync(new object?[] { hash });
        Assert.That(chunk, Is.Not.Null, "Preview chunk row is missing in DB.");
        return chunk!;
    }

    private async Task<FileManifest> GetFileManifestByNodeFileIdAsync(Guid nodeFileId)
    {
        FileManifest? manifest = await DbContext.NodeFiles
            .AsNoTracking()
            .Where(x => x.Id == nodeFileId)
            .Select(x => x.FileManifest)
            .SingleOrDefaultAsync();

        Assert.That(manifest, Is.Not.Null);
        return manifest!;
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

        var createFileRequest = new CreateFileRequest
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
        var info = Image.Identify(imageBytes);
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

    private static byte[] CreateGradientBitmapBytes(int width, int height)
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
        image.SaveAsBmp(ms);
        return ms.ToArray();
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
