// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class SyncChangesTestBase : IntegrationTestBase
    {
        private protected const string Username = "testuser";
        private protected const string Password = "testpassword";

        private protected TestAppFactory? _factory;
        private protected HttpClient? _client;

        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();

            _factory = new TestAppFactory(CreateOverrides());
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        private protected Dictionary<string, string?> CreateOverrides()
        {
            NpgsqlConnectionStringBuilder csb = new NpgsqlConnectionStringBuilder
            {
                Host = TestPostgresHost,
                Port = TestPostgresPort,
                Database = CurrentDatabaseName,
                Username = TestPostgresUsername,
                Password = TestPostgresPassword,
            };

            return new Dictionary<string, string?>
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
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
            };
        }

        private protected async Task<string> SignInAsync(string username = Username, string password = Password)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{Routes.V1.Auth}/login")
            {
                Content = JsonContent.Create(new LoginRequestDto
                {
                    Username = username,
                    Password = password,
                }),
            };
            request.Headers.Add("X-Forwarded-For", "8.8.8.8");

            using HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            TokenPairResponseDto? login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(login, Is.Not.Null);

            UseBearerAuth(login!.AccessToken);
            return login.AccessToken;
        }

        private protected async Task<NodeDto> GetRootAsync()
        {
            NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>($"{Routes.V1.Layouts}/resolver");
            Assert.That(root, Is.Not.Null);
            return root!;
        }

        private protected async Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
        {
            using HttpResponseMessage response = await _client!.PutAsJsonAsync(
                $"{Routes.V1.Layouts}/nodes",
                new CreateNodeRequestDto { ParentId = parentId, Name = name });
            response.EnsureSuccessStatusCode();

            NodeDto? node = await response.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(node, Is.Not.Null);
            return node!;
        }

        private protected async Task<NodeFileManifestDto> CreateFileAsync(Guid nodeId, string name, string body)
        {
            string hash = await UploadChunkAsync(body);
            using HttpResponseMessage response = await _client!.PostAsJsonAsync(
                $"{Routes.V1.Files}/from-chunks",
                new CreateFileFromChunksRequestDto
                {
                    ChunkHashes = [hash],
                    Name = name,
                    ContentType = "application/octet-stream",
                    Hash = hash,
                    NodeId = nodeId,
                });
            response.EnsureSuccessStatusCode();

            NodeFileManifestDto? file = await response.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(file, Is.Not.Null);
            return file!;
        }

        private protected async Task<string> UploadChunkAsync(string body)
        {
            byte[] content = Encoding.UTF8.GetBytes(body);
            string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
            using MultipartFormDataContent form = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") },
                    },
                    "file",
                    "chunk.bin"
                },
                { new StringContent(hash), "hash" },
            };

            using HttpResponseMessage response = await _client!.PostAsync(Routes.V1.Chunks, form);
            response.EnsureSuccessStatusCode();
            return hash;
        }

        private protected void UseBearerAuth(string accessToken)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        private protected async Task<SyncChangesResponseDto> GetChangesAsync(long since, int limit)
        {
            SyncChangesResponseDto? response = await _client!.GetFromJsonAsync<SyncChangesResponseDto>(
                $"{Routes.V1.Sync}/changes?since={since}&limit={limit}");

            Assert.That(response, Is.Not.Null);
            return response!;
        }

        private protected async Task<SyncChangeDto> GetSingleChangeAsync(long cursor, Guid itemId)
        {
            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 20);
            return response.Changes.Single(x => x.ItemId == itemId);
        }
    }
}
