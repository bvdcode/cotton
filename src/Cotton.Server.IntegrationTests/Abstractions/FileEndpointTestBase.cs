// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class FileEndpointTestBase : IntegrationTestBase
    {
        private protected TestAppFactory? _factory;
        private protected HttpClient? _client;

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

            NpgsqlConnectionStringBuilder csb = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 5432,
                Database = DatabaseName,
                Username = "postgres",
                Password = "postgres"
            };
            Dictionary<string, string?> overrides = new Dictionary<string, string?>
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
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        private protected async Task<HttpResponseMessage> UploadRawChunkAsync(string text)
        {
            byte[] content = Encoding.UTF8.GetBytes(text);
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
            return await UploadRawChunkAsync(content, chunkHashLower);
        }

        private protected async Task<HttpResponseMessage> UploadRawChunkAsync(byte[] content, string chunkHashLower)
        {
            using MultipartFormDataContent form = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                    },
                    "file",
                    "chunk.bin"
                },
                { new StringContent(chunkHashLower), "hash" }
            };
            return await _client!.PostAsync("/api/v1/chunks", form);
        }

        private protected async Task<string> UploadChunkAndGetHashAsync(string text)
        {
            HttpResponseMessage response = await UploadRawChunkAsync(text);
            response.EnsureSuccessStatusCode();

            return Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(text)));
        }

        private protected async Task<List<FileVersionDto>> GetVersionsAsync(Guid fileId)
        {
            List<FileVersionDto>? versions = await _client!.GetFromJsonAsync<List<FileVersionDto>>($"/api/v1/files/{fileId}/versions");
            Assert.That(versions, Is.Not.Null);
            return versions!;
        }

        private protected async Task<NodeFileManifestDto> UpdateTextFileAsync(
            NodeFileManifestDto file,
            NodeDto root,
            string text)
        {
            string hash = await UploadChunkAndGetHashAsync(text);
            HttpResponseMessage updateResponse = await _client!.PatchAsJsonAsync($"/api/v1/files/{file.Id}/update-content", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [hash],
                Name = file.Name,
                ContentType = "text/plain",
                Hash = hash,
                NodeId = root.Id,
            });
            updateResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto? updated = await updateResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(updated, Is.Not.Null);
            return updated!;
        }

        private protected async Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
        {
            HttpResponseMessage response = await _client!.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = parentId, Name = name });
            response.EnsureSuccessStatusCode();
            NodeDto? node = await response.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(node, Is.Not.Null);
            return node!;
        }

        private protected async Task<NodeFileManifestDto> UploadTextFileAsync(
            NodeDto root,
            string name,
            string text,
            Dictionary<string, string>? metadata = null,
            string contentType = "text/plain")
        {
            byte[] content = Encoding.UTF8.GetBytes(text);
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
            using MultipartFormDataContent form = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                    },
                    "file",
                    "chunk.bin"
                },
                { new StringContent(chunkHashLower), "hash" }
            };
            HttpResponseMessage upRes = await _client!.PostAsync("/api/v1/chunks", form);
            upRes.EnsureSuccessStatusCode();

            CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [chunkHashLower],
                Name = name,
                ContentType = contentType,
                Hash = chunkHashLower,
                NodeId = root.Id,
                Metadata = metadata
            };
            HttpResponseMessage createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            createFileRes.EnsureSuccessStatusCode();

            NodeContentDto? list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root.Id}/children");
            Assert.That(list, Is.Not.Null);
            NodeFileManifestDto? file = list!.Files.SingleOrDefault(x => x.Name == name);
            Assert.That(file, Is.Not.Null);
            return file!;
        }

        private protected async Task StoreSmallPreviewAsync(Guid nodeFileId, byte[] previewHash, byte[] previewBytes)
        {
            await StorePreviewBytesAsync(previewHash, previewBytes);

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            Guid manifestId = await dbContext.NodeFiles
                .Where(x => x.Id == nodeFileId)
                .Select(x => x.FileManifestId)
                .SingleAsync();

            FileManifest manifest = await dbContext.FileManifests.SingleAsync(x => x.Id == manifestId);
            manifest.SmallFilePreviewHash = previewHash;
            await dbContext.SaveChangesAsync();
        }

        private protected async Task StorePreviewBytesAsync(byte[] previewHash, byte[] previewBytes)
        {
            string previewStorageKey = Hasher.ToHexStringHash(previewHash);
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            IStoragePipeline storage = scope.ServiceProvider.GetRequiredService<IStoragePipeline>();
            await storage.WriteAsync(previewStorageKey, new MemoryStream(previewBytes));
        }

        private protected static byte[] CreateWebpSignatureBytes(string payload)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            byte[] bytes = new byte[12 + payloadBytes.Length];
            Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
            BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
            Encoding.ASCII.GetBytes("WEBP").CopyTo(bytes, 8);
            payloadBytes.CopyTo(bytes, 12);
            return bytes;
        }

        private protected static string ExtractToken(string downloadLink)
        {
            const string marker = "token=";
            int index = downloadLink.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            return Uri.UnescapeDataString(downloadLink[(index + marker.Length)..]);
        }

        private protected async Task<bool> WaitForDownloadTokenAsync(string token, bool expectedExists)
        {
            for (int i = 0; i < 20; i++)
            {
                DbContext.ChangeTracker.Clear();
                bool exists = await DbContext.DownloadTokens.AnyAsync(x => x.Token == token);
                if (exists == expectedExists)
                {
                    return exists;
                }

                await Task.Delay(50);
            }

            DbContext.ChangeTracker.Clear();
            return await DbContext.DownloadTokens.AnyAsync(x => x.Token == token);
        }

        private protected async Task<string> LoginAsync(
            string username = "testuser",
            string password = "testpassword")
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new LoginRequestDto()
                {
                    Username = username,
                    Password = password
                })
            };
            request.Headers.Add("X-Forwarded-For", "8.8.8.8");
            HttpResponseMessage res = await _client!.SendAsync(request);
            res.EnsureSuccessStatusCode();
            TokenPairResponseDto? login = await res.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(login, Is.Not.Null);
            return login!.AccessToken;
        }

        private protected async Task<string> GetWebDavTokenAsync()
        {
            string token = await _client!.GetStringAsync("/api/v1/auth/webdav/token");
            Assert.That(token, Is.Not.Empty);
            return token;
        }
    }
}
