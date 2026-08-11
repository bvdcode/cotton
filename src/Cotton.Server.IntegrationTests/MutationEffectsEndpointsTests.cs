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
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NUnit.Framework;
using Quartz;
using Quartz.Impl.Matchers;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class MutationEffectsEndpointsTests : IntegrationTestBase
    {
        private TestAppFactory? _baseFactory;
        private WebApplicationFactory<Program>? _factory;
        private HttpClient? _client;
        private RecordingEventNotificationService? _notifications;

        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();

            NpgsqlConnectionStringBuilder connection = new()
            {
                Host = TestPostgresHost,
                Port = TestPostgresPort,
                Database = CurrentDatabaseName,
                Username = TestPostgresUsername,
                Password = TestPostgresPassword,
            };
            Dictionary<string, string?> overrides = new()
            {
                ["DatabaseSettings:Host"] = connection.Host,
                ["DatabaseSettings:Port"] = connection.Port.ToString(),
                ["DatabaseSettings:Database"] = connection.Database,
                ["DatabaseSettings:Username"] = connection.Username,
                ["DatabaseSettings:Password"] = connection.Password,
                ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
                ["MasterEncryptionKeyId"] = "1",
                ["EncryptionThreads"] = "1",
                ["MaxChunkSizeBytes"] = "16777216",
                ["CipherChunkSizeBytes"] = "20971520",
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
            };

            _notifications = new RecordingEventNotificationService();
            _baseFactory = new TestAppFactory(overrides);
            _factory = _baseFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEventNotificationService>();
                    services.AddSingleton<IEventNotificationService>(_notifications);
                });
            });
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
            _baseFactory?.Dispose();
        }

        [Test]
        public async Task MutationEndpoints_PublishEachEffectOnce()
        {
            await AuthenticateAsync();
            HttpClient client = _client!;
            NodeDto root = (await client.GetFromJsonAsync<NodeDto>(
                "/api/v1/layouts/resolver"))!;
            IScheduler scheduler = await _factory!.Services
                .GetRequiredService<ISchedulerFactory>()
                .GetScheduler();
            int initialTriggerCount = await GetTriggerCountAsync(scheduler);

            NodeDto node = await CreateNodeAsync(root.Id, "effects");
            using HttpResponseMessage renameNode = await client.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{node.Id}/rename",
                new { name = "renamed-effects" });
            renameNode.EnsureSuccessStatusCode();
            using HttpResponseMessage updateNodeMetadata = await client.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{node.Id}/metadata",
                new Dictionary<string, string> { ["color"] = "blue" });
            updateNodeMetadata.EnsureSuccessStatusCode();

            NodeFileManifestDto file = await CreateFileAsync(node.Id, "effects.txt", "initial");
            Assert.That(await GetTriggerCountAsync(scheduler), Is.EqualTo(initialTriggerCount + 3));

            using HttpResponseMessage renameFile = await client.PatchAsJsonAsync(
                $"/api/v1/files/{file.Id}/rename",
                new { name = "renamed-effects.txt" });
            renameFile.EnsureSuccessStatusCode();
            using HttpResponseMessage updateFileMetadata = await client.PatchAsJsonAsync(
                $"/api/v1/files/{file.Id}/metadata",
                new Dictionary<string, string> { ["color"] = "green" });
            updateFileMetadata.EnsureSuccessStatusCode();

            string updatedHash = await UploadChunkAsync("updated");
            using HttpResponseMessage updateFile = await client.PatchAsJsonAsync(
                $"/api/v1/files/{file.Id}/update-content",
                new CreateFileFromChunksRequestDto
                {
                    ChunkHashes = [updatedHash],
                    Name = "renamed-effects.txt",
                    ContentType = "text/plain",
                    Hash = updatedHash,
                    NodeId = node.Id,
                });
            updateFile.EnsureSuccessStatusCode();
            Assert.That(await GetTriggerCountAsync(scheduler), Is.EqualTo(initialTriggerCount + 6));

            using HttpResponseMessage deleteFile = await client.DeleteAsync($"/api/v1/files/{file.Id}");
            deleteFile.EnsureSuccessStatusCode();
            using HttpResponseMessage restoreFile = await client.PostAsJsonAsync(
                $"/api/v1/files/{file.Id}/restore",
                new RestoreItemRequestDto());
            restoreFile.EnsureSuccessStatusCode();

            using HttpResponseMessage deleteNode = await client.DeleteAsync($"/api/v1/layouts/nodes/{node.Id}");
            deleteNode.EnsureSuccessStatusCode();
            using HttpResponseMessage restoreNode = await client.PostAsJsonAsync(
                $"/api/v1/layouts/nodes/{node.Id}/restore",
                new RestoreItemRequestDto());
            restoreNode.EnsureSuccessStatusCode();

            Assert.Multiple(() =>
            {
                Assert.That(_notifications!.FileCreatedCount, Is.EqualTo(1));
                Assert.That(_notifications.FileUpdatedCount, Is.EqualTo(2));
                Assert.That(_notifications.FileDeletedCount, Is.EqualTo(1));
                Assert.That(_notifications.FileRenamedCount, Is.EqualTo(1));
                Assert.That(_notifications.FileRestoredCount, Is.EqualTo(1));
                Assert.That(_notifications.NodeCreatedCount, Is.EqualTo(1));
                Assert.That(_notifications.NodeDeletedCount, Is.EqualTo(1));
                Assert.That(_notifications.NodeRenamedCount, Is.EqualTo(1));
                Assert.That(_notifications.NodeMetadataUpdatedCount, Is.EqualTo(1));
                Assert.That(_notifications.NodeRestoredCount, Is.EqualTo(1));
            });
        }

        private async Task AuthenticateAsync()
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new LoginRequestDto
                {
                    Username = "testuser",
                    Password = "testpassword",
                }),
            };
            request.Headers.Add("X-Forwarded-For", "8.8.8.8");
            using HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();
            TokenPairResponseDto login = (await response.Content
                .ReadFromJsonAsync<TokenPairResponseDto>())!;
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);
        }

        private async Task<NodeDto> CreateNodeAsync(Guid parentId, string name)
        {
            using HttpResponseMessage response = await _client!.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = parentId, Name = name });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<NodeDto>())!;
        }

        private async Task<NodeFileManifestDto> CreateFileAsync(
            Guid nodeId,
            string name,
            string body)
        {
            string hash = await UploadChunkAsync(body);
            using HttpResponseMessage response = await _client!.PostAsJsonAsync(
                "/api/v1/files/from-chunks",
                new CreateFileFromChunksRequestDto
                {
                    ChunkHashes = [hash],
                    Name = name,
                    ContentType = "text/plain",
                    Hash = hash,
                    NodeId = nodeId,
                });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
        }

        private async Task<string> UploadChunkAsync(string body)
        {
            byte[] content = Encoding.UTF8.GetBytes(body);
            string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
            using MultipartFormDataContent form = new()
            {
                { new ByteArrayContent(content), "file", "chunk.bin" },
                { new StringContent(hash), "hash" },
            };
            using HttpResponseMessage response = await _client!.PostAsync("/api/v1/chunks", form);
            response.EnsureSuccessStatusCode();
            return hash;
        }

        private static async Task<int> GetTriggerCountAsync(IScheduler scheduler)
        {
            IReadOnlyCollection<TriggerKey> keys = await scheduler.GetTriggerKeys(
                GroupMatcher<TriggerKey>.AnyGroup());
            return keys.Count;
        }
    }
}
