// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    public abstract class LayoutFileTestBase : IntegrationTestBase
    {
        private protected TestAppFactory? _factory;
        private protected HttpClient? _client;

        [SetUp]
        public void SetUp()
        {
            // Reset DB to empty state
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();
            Assert.Multiple(() =>
            {
                Assert.That(creator.Exists(), Is.True, "DB must exist after Create()");
                Assert.That(creator.HasTables(), Is.False, "DB must have no user tables after Create()");
            });

            // Build connection overrides
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
        }

        private protected async Task<NodeDto> CreateNodeAsync(Guid parentId, string name)
        {
            HttpResponseMessage response = await _client!.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = parentId, Name = name });
            response.EnsureSuccessStatusCode();
            NodeDto? node = await response.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(node, Is.Not.Null);
            return node!;
        }

        private protected async Task<NodeFileManifestDto> UploadTextFileAsync(Guid nodeId, string name, string body)
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
                    $"{name}.chunk"
                },
                { new StringContent(hash), "hash" }
            };

            HttpResponseMessage uploadResponse = await _client!.PostAsync("/api/v1/chunks", form);
            uploadResponse.EnsureSuccessStatusCode();

            CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [hash],
                Name = name,
                ContentType = "text/plain",
                Hash = hash,
                NodeId = nodeId
            };
            HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            createResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto? file = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(file, Is.Not.Null);
            return file!;
        }

        private protected async Task<string> LoginAsync()
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
            HttpResponseMessage res = await _client!.SendAsync(request);
            res.EnsureSuccessStatusCode();
            TokenPairResponseDto? login = await res.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(login, Is.Not.Null);
            await TestContext.Progress.WriteLineAsync(
                $"Login OK. Token: {login!.AccessToken[..Math.Min(16, login.AccessToken.Length)]}...");
            return login.AccessToken;
        }
    }
}
