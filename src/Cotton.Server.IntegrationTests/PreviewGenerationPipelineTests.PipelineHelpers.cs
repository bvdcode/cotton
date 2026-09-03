// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class PreviewGenerationPipelineTests
    {
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
            using MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }

        private async Task<NodeFileManifestDto> UploadAndCreateFileAsync(Guid nodeId, string fileName, string contentType, byte[] content)
        {
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));

            using MultipartFormDataContent uploadForm = new MultipartFormDataContent
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

            CreateFileFromChunksRequestDto createFileRequest = new CreateFileFromChunksRequestDto
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
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
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

    }
}
