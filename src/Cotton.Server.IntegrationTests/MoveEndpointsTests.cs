// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;

namespace Cotton.Server.IntegrationTests
{
    public partial class MoveEndpointsTests : IntegrationTestBase
    {
        private TestAppFactory? _factory;
        private HttpClient? _client;
        private Dictionary<string, string?> _overrides = new();

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

        private static async Task<bool> ParentWalkReachesRoot(CottonDbContext db, Guid startId)
        {
            HashSet<Guid> seen = new HashSet<Guid>();
            Guid? current = startId;
            while (current.HasValue)
            {
                if (!seen.Add(current.Value)) return false;
                if (seen.Count > 1024) return false;
                current = await db.Nodes
                    .AsNoTracking()
                    .Where(n => n.Id == current.Value)
                    .Select(n => n.ParentId)
                    .SingleOrDefaultAsync();
            }
            return true;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private CottonDbContext NewReadOnlyDbContext()
        {
            DbContextOptionsBuilder<CottonDbContext> optionsBuilder = new DbContextOptionsBuilder<CottonDbContext>();
            NpgsqlConnectionStringBuilder csb = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 5432,
                Database = DatabaseName,
                Username = "postgres",
                Password = "postgres",
                // Disable pooling so each test sees a fresh connection — between tests we
                // recreate the schema (EnsureDeleted + Create + migrations) and Postgres
                // type OIDs may change, which trips cached type lookups otherwise.
                Pooling = false,
            };
            optionsBuilder.UseNpgsql(csb.ConnectionString);
            return new CottonDbContext(optionsBuilder.Options);
        }

        private async Task AuthenticateAsync()
        {
            string token = await LoginViaClientAsync(_client!);
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private static async Task<string> LoginViaClientAsync(HttpClient client)
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

        private static async Task UseWebDavBasicAuthAsync(HttpClient client)
        {
            string webDavToken = await client.GetStringAsync("/api/v1/auth/webdav/token");
            Assert.That(webDavToken, Is.Not.Empty);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{webDavToken}")));
        }

        private async Task<NodeDto> GetRootAsync()
        {
            NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            return root!;
        }

        private Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
            => CreateFolderViaClientAsync(_client!, parentId, name);

        private static async Task<NodeDto> CreateFolderViaClientAsync(HttpClient client, Guid parentId, string name)
        {
            HttpResponseMessage res = await client.PutAsJsonAsync("/api/v1/layouts/nodes", new CreateNodeRequestDto { ParentId = parentId, Name = name });
            res.EnsureSuccessStatusCode();
            NodeDto? node = await res.Content.ReadFromJsonAsync<NodeDto>();
            return node!;
        }

        private Task<NodeFileManifestDto> CreateFileAsync(Guid nodeId, string name, string body)
            => CreateFileViaClientAsync(_client!, nodeId, name, body);

        private static async Task<NodeFileManifestDto> CreateFileViaClientAsync(HttpClient client, Guid nodeId, string name, string body)
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

        private static Task<HttpResponseMessage> SendUpdateFileViaClientAsync(
            HttpClient client,
            Guid nodeFileId,
            Guid nodeId,
            string name,
            string hash)
        {
            CreateFileFromChunksRequestDto request = new()
            {
                ChunkHashes = [hash],
                Name = name,
                ContentType = "application/octet-stream",
                Hash = hash,
                NodeId = nodeId,
            };
            return client.PatchAsJsonAsync($"/api/v1/files/{nodeFileId}/update-content", request);
        }

        private static async Task<(Guid OwnerId, Guid RootId)> CreateAdditionalLayoutRootAsync(
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

        private static async Task<Guid> CreateEmptyFileAsync(
            IServiceProvider services,
            Guid ownerId,
            Guid nodeId,
            string name)
        {
            using IServiceScope scope = services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = new()
            {
                ProposedContentHash = Hasher.HashData(Guid.NewGuid().ToByteArray()),
                ContentType = "application/octet-stream",
                SizeBytes = 0,
            };
            NodeFile nodeFile = new()
            {
                OwnerId = ownerId,
                NodeId = nodeId,
                FileManifest = manifest,
            };
            nodeFile.OriginalNodeFileId = nodeFile.Id;
            nodeFile.SetName(name);
            dbContext.NodeFiles.Add(nodeFile);
            await dbContext.SaveChangesAsync();
            return nodeFile.Id;
        }

        private static async Task<string> UploadChunkViaClientAsync(HttpClient client, string body)
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

        private static async Task<HttpResponseMessage> SendWebDavPutAsync(HttpClient client, string path, string body)
        {
            using StringContent content = new StringContent(body, Encoding.UTF8, "text/plain");
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = content
            };
            return await client.SendAsync(request);
        }

        private static async Task<HttpResponseMessage> SendWebDavMkColAsync(HttpClient client, string path)
        {
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("MKCOL"), path);
            return await client.SendAsync(request);
        }

        private async Task<NodeContentDto> GetChildrenAsync(Guid nodeId)
        {
            NodeContentDto? res = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
            return res!;
        }

        private Task<HttpResponseMessage> MoveFileAsync(Guid fileId, Guid parentId)
            => _client!.PatchAsJsonAsync($"/api/v1/files/{fileId}/move", new MoveFileRequestDto { ParentId = parentId });

        private Task<HttpResponseMessage> MoveNodeAsync(Guid nodeId, Guid parentId)
            => _client!.PatchAsJsonAsync($"/api/v1/layouts/nodes/{nodeId}/move", new MoveNodeRequestDto { ParentId = parentId });
    }

}
