// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sync;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public partial class SyncChangesEndpointsTests : IntegrationTestBase
    {
        private const string Username = "testuser";
        private const string Password = "testpassword";

        private TestAppFactory? _factory;
        private HttpClient? _client;

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

        private Dictionary<string, string?> CreateOverrides()
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

        private async Task<string> SignInAsync(string username = Username, string password = Password)
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

        private async Task<Guid> GetUserIdAsync(string username)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            User user = await dbContext.Users
                .AsNoTracking()
                .SingleAsync(x => x.Username == username);

            return user.Id;
        }

        private async Task CreateUserAsync(string username, string password)
        {
            using HttpResponseMessage response = await _client!.PostAsJsonAsync(Routes.V1.Users, new
            {
                username,
                password,
                role = UserRole.User,
            });

            response.EnsureSuccessStatusCode();
        }

        private async Task<NodeDto> GetRootAsync()
        {
            NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>($"{Routes.V1.Layouts}/resolver");
            Assert.That(root, Is.Not.Null);
            return root!;
        }

        private async Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
        {
            using HttpResponseMessage response = await _client!.PutAsJsonAsync(
                $"{Routes.V1.Layouts}/nodes",
                new CreateNodeRequestDto { ParentId = parentId, Name = name });
            response.EnsureSuccessStatusCode();

            NodeDto? node = await response.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(node, Is.Not.Null);
            return node!;
        }

        private async Task<NodeFileManifestDto> CreateFileAsync(Guid nodeId, string name, string body)
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

        private async Task<NodeFileManifestDto> UpdateFileContentAsync(Guid nodeFileId, Guid nodeId, string name, string body)
        {
            string hash = await UploadChunkAsync(body);
            using HttpResponseMessage response = await _client!.PatchAsJsonAsync(
                $"{Routes.V1.Files}/{nodeFileId}/update-content",
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

        private async Task<List<FileVersionDto>> GetFileVersionsAsync(Guid nodeFileId)
        {
            List<FileVersionDto>? versions = await _client!.GetFromJsonAsync<List<FileVersionDto>>(
                $"{Routes.V1.Files}/{nodeFileId}/versions");

            Assert.That(versions, Is.Not.Null);
            return versions!;
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

            using HttpResponseMessage response = await _client!.PostAsync(Routes.V1.Chunks, form);
            response.EnsureSuccessStatusCode();
            return hash;
        }

        private void UseBearerAuth(string accessToken)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        private async Task UseWebDavBasicAuthAsync()
        {
            string webDavToken = await _client!.GetStringAsync("/api/v1/auth/webdav/token");
            Assert.That(webDavToken, Is.Not.Empty);
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{webDavToken}")));
        }

        private async Task<HttpResponseMessage> SendWebDavPutAsync(string path, string body)
        {
            using StringContent content = new StringContent(body, Encoding.UTF8, "text/plain");
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = content,
            };

            return await _client!.SendAsync(request);
        }

        private async Task<HttpResponseMessage> SendWebDavMkColAsync(string path)
        {
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("MKCOL"), path);
            return await _client!.SendAsync(request);
        }

        private async Task<HttpResponseMessage> SendWebDavMoveAsync(string sourcePath, string destinationPath)
        {
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("MOVE"), sourcePath);
            request.Headers.Add("Destination", destinationPath);
            request.Headers.Add("Overwrite", "F");
            return await _client!.SendAsync(request);
        }

        private async Task<HttpResponseMessage> SendWebDavCopyAsync(string sourcePath, string destinationPath)
        {
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("COPY"), sourcePath);
            request.Headers.Add("Destination", destinationPath);
            request.Headers.Add("Overwrite", "F");
            return await _client!.SendAsync(request);
        }

        private async Task<long> AddSyncChangeAsync(Guid ownerId, string name)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            SyncChange change = new SyncChange
            {
                OwnerId = ownerId,
                Kind = SyncChangeKind.FileCreated,
                LayoutId = Guid.NewGuid(),
                ItemId = Guid.NewGuid(),
                ParentNodeId = Guid.NewGuid(),
                Name = name,
            };

            dbContext.SyncChanges.Add(change);
            await dbContext.SaveChangesAsync();
            return change.Id;
        }

        private async Task DeleteSyncChangeAsync(long id)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            await dbContext.SyncChanges
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync();
        }

        private async Task SetSyncChangeCreatedAtAsync(long id, DateTime createdAt)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            await dbContext.SyncChanges
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(change => change.CreatedAt, createdAt)
                    .SetProperty(change => change.UpdatedAt, createdAt));
        }

        private async Task<int> RunSyncRetentionAsync(DateTime cutoff)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            SyncChangeRetentionJob job = new SyncChangeRetentionJob(
                dbContext,
                NullLogger<SyncChangeRetentionJob>.Instance);

            return await job.DeleteExpiredChangesAsync(cutoff, CancellationToken.None);
        }

        private async Task<List<long>> GetSyncChangeIdsAsync(Guid ownerId)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            return await dbContext.SyncChanges
                .AsNoTracking()
                .Where(x => x.OwnerId == ownerId)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();
        }

        private async Task<SyncChangesResponseDto> GetChangesAsync(long since, int limit)
        {
            SyncChangesResponseDto? response = await _client!.GetFromJsonAsync<SyncChangesResponseDto>(
                $"{Routes.V1.Sync}/changes?since={since}&limit={limit}");

            Assert.That(response, Is.Not.Null);
            return response!;
        }

        private async Task<SyncChangeDto> GetSingleChangeAsync(long cursor, Guid itemId)
        {
            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 20);
            return response.Changes.Single(x => x.ItemId == itemId);
        }
    }
}
