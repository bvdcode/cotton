// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class MoveEndpointTestBase : IntegrationTestBase
    {
        private protected TestAppFactory? _factory;
        private protected HttpClient? _client;
        private protected Dictionary<string, string?> _overrides = new();

        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();

            NpgsqlConnectionStringBuilder csb = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 5432,
                Database = DatabaseName,
                Username = "postgres",
                Password = "postgres"
            };
            _overrides = new Dictionary<string, string?>
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

            _factory = new TestAppFactory(_overrides);
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        private protected async Task AuthenticateAsync()
        {
            string token = await LoginViaClientAsync(_client!);
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private protected static async Task<string> LoginViaClientAsync(HttpClient client)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new LoginRequestDto()
                {
                    Username = "testuser",
                    Password = "testpassword"
                })
            };
            request.Headers.Add("X-Forwarded-For", "8.8.8.8");
            HttpResponseMessage res = await client.SendAsync(request);
            res.EnsureSuccessStatusCode();
            TokenPairResponseDto? login = await res.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            return login!.AccessToken;
        }

        private protected static async Task UseWebDavBasicAuthAsync(HttpClient client)
        {
            string webDavToken = await client.GetStringAsync("/api/v1/auth/webdav/token");
            Assert.That(webDavToken, Is.Not.Empty);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{webDavToken}")));
        }

        private protected static async Task AssertConflictKindAsync(
            HttpResponseMessage response,
            RestoreConflictKind expectedKind)
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            JsonElement payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(
                payload.GetProperty("conflictKind").GetString(),
                Is.EqualTo(expectedKind.ToString()));
        }

        private protected async Task<NodeDto> GetRootAsync()
        {
            NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            return root!;
        }

        private protected Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
            => CreateFolderViaClientAsync(_client!, parentId, name);

        private protected static async Task<NodeDto> CreateFolderViaClientAsync(HttpClient client, Guid parentId, string name)
        {
            HttpResponseMessage res = await client.PutAsJsonAsync("/api/v1/layouts/nodes", new CreateNodeRequestDto { ParentId = parentId, Name = name });
            res.EnsureSuccessStatusCode();
            NodeDto? node = await res.Content.ReadFromJsonAsync<NodeDto>();
            return node!;
        }

        private protected Task<NodeFileManifestDto> CreateFileAsync(Guid nodeId, string name, string body)
            => CreateFileViaClientAsync(_client!, nodeId, name, body);

        private protected static async Task<NodeFileManifestDto> CreateFileViaClientAsync(HttpClient client, Guid nodeId, string name, string body)
        {
            string hash = await UploadChunkViaClientAsync(client, body);
            CreateFileFromChunksRequestDto request = new()
            {
                ChunkHashes = [hash],
                Name = name,
                ContentType = "application/octet-stream",
                Hash = hash,
                NodeId = nodeId,
            };
            using HttpResponseMessage createRes = await client.PostAsJsonAsync("/api/v1/files/from-chunks", request);
            createRes.EnsureSuccessStatusCode();

            // Read back from the folder so callers get the same projection as the files UI.
            NodeContentDto? children = await client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
            NodeFileManifestDto dto = children!.Files.SingleOrDefault(f => f.Name == name)
                ?? throw new InvalidOperationException($"Created file '{name}' not found in node {nodeId}.");
            return dto;
        }

        private protected static async Task<(Guid OwnerId, Guid RootId)> CreateAdditionalLayoutRootAsync(
            IServiceProvider services,
            string rootName)
        {
            using IServiceScope scope = services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Guid ownerId = await dbContext.Users.AsNoTracking().Select(user => user.Id).FirstAsync();
            Cotton.Database.Models.Layout layout = new()
            {
                OwnerId = ownerId,
                IsActive = false,
            };
            dbContext.UserLayouts.Add(layout);
            await dbContext.SaveChangesAsync();

            Cotton.Database.Models.Node root = new()
            {
                LayoutId = layout.Id,
                OwnerId = ownerId,
                Type = Cotton.Database.Models.Enums.NodeType.Default,
                ParentId = null,
            };
            root.SetName(rootName);
            dbContext.Nodes.Add(root);
            await dbContext.SaveChangesAsync();
            return (ownerId, root.Id);
        }

        private protected static async Task<string> UploadChunkViaClientAsync(HttpClient client, string body)
        {
            byte[] content = Encoding.UTF8.GetBytes(body);
            string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
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
                { new StringContent(hash), "hash" }
            };
            HttpResponseMessage upRes = await client.PostAsync("/api/v1/chunks", form);
            upRes.EnsureSuccessStatusCode();
            return hash;
        }

        private protected async Task<NodeContentDto> GetChildrenAsync(Guid nodeId)
        {
            NodeContentDto? res = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
            return res!;
        }

        private protected Task<HttpResponseMessage> MoveFileAsync(Guid fileId, Guid parentId)
            => MoveFileAsync(fileId, new MoveFileRequestDto { ParentId = parentId });

        private protected Task<HttpResponseMessage> MoveFileAsync(Guid fileId, MoveFileRequestDto request)
            => _client!.PatchAsJsonAsync($"/api/v1/files/{fileId}/move", request);

        private protected Task<HttpResponseMessage> MoveNodeAsync(Guid nodeId, Guid parentId)
            => MoveNodeAsync(nodeId, new MoveNodeRequestDto { ParentId = parentId });

        private protected Task<HttpResponseMessage> MoveNodeAsync(Guid nodeId, MoveNodeRequestDto request)
            => _client!.PatchAsJsonAsync($"/api/v1/layouts/nodes/{nodeId}/move", request);
    }
}
