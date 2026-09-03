// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public partial class LayoutEndpointsTests : IntegrationTestBase
    {
        private TestAppFactory? _factory;
        private HttpClient? _client;

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

        private async Task<(SearchResultDto Result, int TotalCount)> SearchAsync(Guid layoutId, string query, int page = 1, int pageSize = 20)
        {
            HttpResponseMessage response = await _client!.GetAsync(
                $"/api/v1/layouts/{layoutId}/search?query={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}");
            response.EnsureSuccessStatusCode();
            SearchResultDto result = (await response.Content.ReadFromJsonAsync<SearchResultDto>())!;
            int totalCount = int.Parse(response.Headers.GetValues("X-Total-Count").Single());
            return (result, totalCount);
        }

        private async Task<NodeDto> CreateNodeAsync(Guid parentId, string name)
        {
            HttpResponseMessage response = await _client!.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = parentId, Name = name });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<NodeDto>())!;
        }

        private async Task<NodeFileManifestDto> CreateFileAsync(
            Guid nodeId,
            string name,
            string body,
            string contentType = "application/octet-stream")
        {
            string hash = await UploadChunkAsync(body);
            CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [hash],
                Name = name,
                ContentType = contentType,
                Hash = hash,
                NodeId = nodeId,
            };
            HttpResponseMessage response = await _client!.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            response.EnsureSuccessStatusCode();

            NodeContentDto? children = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
            return children!.Files.Single(x => x.Name == name);
        }

        private async Task<string> UploadChunkAsync(string body)
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

            HttpResponseMessage response = await _client!.PostAsync("/api/v1/chunks", form);
            response.EnsureSuccessStatusCode();
            return hash;
        }

        private async Task<string> LoginAsync()
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
            return login!.AccessToken;
        }
    }
}
