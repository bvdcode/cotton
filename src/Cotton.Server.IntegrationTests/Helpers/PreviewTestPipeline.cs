// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Previews;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class PreviewTestPipeline(TestAppFactory factory, HttpClient client, CottonDbContext dbContext)
    {
        public async Task ExecuteGeneratePreviewJobAsync()
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            GeneratePreviewJob job = ActivatorUtilities.CreateInstance<GeneratePreviewJob>(scope.ServiceProvider);
            await job.Execute(null!);
        }

        public async Task ExecuteExtractFileMetadataJobAsync()
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            ExtractFileMetadataJob job = ActivatorUtilities.CreateInstance<ExtractFileMetadataJob>(scope.ServiceProvider);
            await job.Execute(null!);
        }

        public async Task UpdateFileManifestAsync(Guid nodeFileId, Action<FileManifest> update)
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await dbContext.NodeFiles
                .Where(x => x.Id == nodeFileId)
                .Select(x => x.FileManifest)
                .SingleAsync();

            update(manifest);
            await dbContext.SaveChangesAsync();
        }

        public async Task<FileManifestMetadataState> GetFileManifestMetadataStateAsync(Guid nodeFileId)
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            return await dbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == nodeFileId)
                .Select(x => new FileManifestMetadataState(
                    x.FileManifest.Metadata))
                .SingleAsync();
        }

        public async Task<Chunk> GetChunkByHashAsync(byte[] hash)
        {
            Chunk? chunk = await dbContext.Chunks.FindAsync([hash]);
            Assert.That(chunk, Is.Not.Null, "Preview chunk row is missing in DB.");
            return chunk!;
        }

        public async Task<FileManifestPreviewState> GetFileManifestByNodeFileIdAsync(Guid nodeFileId)
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
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

        public async Task<byte[]> ReadPreviewBlobAsync(byte[] hash)
        {
            string storageKey = Hasher.ToHexStringHash(hash);

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            IStoragePipeline storage = scope.ServiceProvider.GetRequiredService<IStoragePipeline>();

            await using Stream stream = await storage.ReadAsync(storageKey);
            using MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }

        public async Task<NodeFileManifestDto> UploadAndCreateFileAsync(Guid nodeId, string fileName, string contentType, byte[] content)
        {
            const int uploadChunkSizeBytes = 4 * 1024 * 1024;
            List<string> chunkHashes = [];
            for (int offset = 0; offset < Math.Max(content.Length, 1); offset += uploadChunkSizeBytes)
            {
                byte[] chunk = content.AsSpan(offset, Math.Min(uploadChunkSizeBytes, content.Length - offset)).ToArray();
                string chunkHash = Hasher.ToHexStringHash(Hasher.HashData(chunk));
                using MultipartFormDataContent uploadForm = new MultipartFormDataContent
                {
                    {
                        new ByteArrayContent(chunk)
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
                        new StringContent(chunkHash),
                        "hash"
                    }
                };

                HttpResponseMessage uploadResponse = await client.PostAsync("/api/v1/chunks", uploadForm);
                uploadResponse.EnsureSuccessStatusCode();
                chunkHashes.Add(chunkHash);
            }

            CreateFileFromChunksRequestDto createFileRequest = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = chunkHashes,
                Name = fileName,
                ContentType = contentType,
                Hash = Hasher.ToHexStringHash(Hasher.HashData(content)),
                NodeId = nodeId,
            };

            HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/v1/files/from-chunks", createFileRequest);
            createResponse.EnsureSuccessStatusCode();

            return await GetNodeFileAsync(nodeId, fileName);
        }

        public async Task<NodeFileManifestDto> GetNodeFileAsync(Guid nodeId, string fileName)
        {
            NodeContentDto? content = await client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
            Assert.That(content, Is.Not.Null);

            NodeFileManifestDto? file = content!.Files.SingleOrDefault(x => x.Name == fileName);
            Assert.That(file, Is.Not.Null, $"Node file '{fileName}' was not found in node {nodeId}.");
            return file!;
        }

        public async Task<NodeDto> GetRootNodeAsync()
        {
            NodeDto? root = await client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            return root!;
        }

        public async Task<string> LoginAsync()
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

            HttpResponseMessage response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            TokenPairResponseDto? payload = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(payload, Is.Not.Null);

            return payload!.AccessToken;
        }

        public void SetBearer(string accessToken)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        public async Task RenamePreviewFileAsync(Guid fileId, string name)
        {
            using HttpResponseMessage response = await client.PatchAsJsonAsync(
                $"/api/v1/files/{fileId}/rename", new RenameFileRequestDto { Name = name });
            response.EnsureSuccessStatusCode();
        }

        public async Task<Guid[]> GetPendingPreviewIdsAsync()
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            List<Guid> items = await PreviewQueueLoader.LoadNextIdsAsync(dbContext, 100, new HashSet<Guid>(), CancellationToken.None);
            return [.. items];
        }

        public static async Task<FileManifest> LoadFileManifestAsync(
            CottonDbContext dbContext,
            Guid nodeFileId)
        {
            return await dbContext.NodeFiles
                .Where(x => x.Id == nodeFileId)
                .Select(x => x.FileManifest)
                .SingleAsync();
        }

        public static string? GetPreviewHashEncryptedHex(Guid manifestId, byte[]? encryptedHash)
        {
            return encryptedHash is null
                ? null
                : string.Concat(FileManifest.PreviewTokenPrefix, manifestId.ToString("N"), Convert.ToHexStringLower(encryptedHash));
        }

        public static async Task ExecutePreviewWithStorageAsync(IServiceProvider services, IStoragePipeline storage)
        {
            FilePreviewRenderer renderer = ActivatorUtilities.CreateInstance<FilePreviewRenderer>(services, storage);
            GeneratePreviewJob job = ActivatorUtilities.CreateInstance<GeneratePreviewJob>(services, storage, renderer);
            await job.Execute(null!);
        }
    }
}
