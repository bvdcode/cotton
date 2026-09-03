// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
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
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests
{
    public class UserManagementEndpointsTests : IntegrationTestBase
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

        [Test]
        public async Task Admin_CreateUser_CreatesUser_AndNormalizesUsername()
        {
            string token = await LoginAsync();
            SetBearer(token);

            HttpResponseMessage createResponse = await _client!.PostAsJsonAsync(
                "/api/v1/users",
                new
                {
                    Username = "  New.User-1  ",
                    Email = "  new.user@example.com  ",
                    Password = "UserPass_123",
                    Role = UserRole.User
                });

            createResponse.EnsureSuccessStatusCode();

            UserDto? created = await createResponse.Content.ReadFromJsonAsync<UserDto>();
            Assert.That(created, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(created!.Username, Is.EqualTo("new.user-1"));
                Assert.That(created.Email, Is.EqualTo("new.user@example.com"));
                Assert.That(created.Role, Is.EqualTo((int)UserRole.User));
            });
        }

        [TestCase("ab", "ab")]
        [TestCase("john_doe", "john_doe")]
        [TestCase("john.doe", "john.doe")]
        [TestCase("john-doe", "john-doe")]
        [TestCase("  MiXeD.Name-1  ", "mixed.name-1")]
        public async Task Admin_CreateUser_WithValidUsername_ReturnsSuccess(string username, string expectedNormalized)
        {
            string token = await LoginAsync();
            SetBearer(token);

            HttpResponseMessage createResponse = await _client!.PostAsJsonAsync(
                "/api/v1/users",
                new
                {
                    Username = username,
                    Email = $"{Guid.NewGuid():N}@example.com",
                    Password = "UserPass_123",
                    Role = UserRole.User
                });

            createResponse.EnsureSuccessStatusCode();

            UserDto? created = await createResponse.Content.ReadFromJsonAsync<UserDto>();
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.Username, Is.EqualTo(expectedNormalized));
        }

        [TestCase("1bad")]
        [TestCase("ab__cd")]
        [TestCase("a")]
        public async Task Admin_CreateUser_WithInvalidUsername_ReturnsBadRequest(string invalidUsername)
        {
            string token = await LoginAsync();
            SetBearer(token);

            HttpResponseMessage createResponse = await _client!.PostAsJsonAsync(
                "/api/v1/users",
                new
                {
                    Username = invalidUsername,
                    Email = "invalid.user@example.com",
                    Password = "UserPass_123",
                    Role = UserRole.User
                });

            Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Admin_UpdateUser_UpdatesEditableFields()
        {
            string token = await LoginAsync();
            SetBearer(token);

            UserDto created = await CreateUserAsync("edituser1", "edit.user1@example.com");

            HttpResponseMessage updateResponse = await _client!.PutAsJsonAsync(
                $"/api/v1/users/{created.Id}",
                new
                {
                    Username = "updateduser",
                    Email = "updated.user@example.com",
                    Role = UserRole.User,
                    FirstName = "John",
                    LastName = "Doe",
                    BirthDate = new DateOnly(1990, 5, 10),
                    IsEmailVerified = true
                });

            updateResponse.EnsureSuccessStatusCode();

            AdminUserDto? updated = await updateResponse.Content.ReadFromJsonAsync<AdminUserDto>();
            Assert.That(updated, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(updated!.Id, Is.EqualTo(created.Id));
                Assert.That(updated.Username, Is.EqualTo("updateduser"));
                Assert.That(updated.Email, Is.EqualTo("updated.user@example.com"));
                Assert.That(updated.FirstName, Is.EqualTo("John"));
                Assert.That(updated.LastName, Is.EqualTo("Doe"));
                Assert.That(updated.BirthDate, Is.EqualTo(new DateOnly(1990, 5, 10)));
                Assert.That(updated.Role, Is.EqualTo(UserRole.User));
            });
        }

        [TestCase("validuser2")]
        [TestCase("john99")]
        [TestCase("az")]
        public async Task Admin_UpdateUser_WithAlphanumericUsername_ReturnsSuccess(string validUsername)
        {
            string token = await LoginAsync();
            SetBearer(token);

            UserDto created = await CreateUserAsync("updatebase", "update.base@example.com");

            HttpResponseMessage updateResponse = await _client!.PutAsJsonAsync(
                $"/api/v1/users/{created.Id}",
                new
                {
                    Username = validUsername,
                    Email = "updated.valid@example.com",
                    Role = UserRole.User,
                    FirstName = "Valid",
                    LastName = "Name",
                    BirthDate = new DateOnly(1999, 1, 1),
                    IsEmailVerified = false
                });

            updateResponse.EnsureSuccessStatusCode();

            AdminUserDto? updated = await updateResponse.Content.ReadFromJsonAsync<AdminUserDto>();
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Username, Is.EqualTo(validUsername));
        }

        [TestCase("user_name", "user_name")]
        [TestCase("user.name", "user.name")]
        [TestCase("user-name", "user-name")]
        [TestCase("  MiXeD_Name.1  ", "mixed_name.1")]
        public async Task Admin_UpdateUser_WithValidUsernameSeparators_ReturnsSuccess(
            string username,
            string expectedNormalized)
        {
            string token = await LoginAsync();
            SetBearer(token);

            UserDto created = await CreateUserAsync("underscoretarget", "underscore.target@example.com");

            HttpResponseMessage updateResponse = await _client!.PutAsJsonAsync(
                $"/api/v1/users/{created.Id}",
                new
                {
                    Username = username,
                    Email = "underscore.target@example.com",
                    Role = UserRole.User,
                    FirstName = "Under",
                    LastName = "Score",
                    BirthDate = new DateOnly(1996, 6, 6),
                    IsEmailVerified = false
                });

            updateResponse.EnsureSuccessStatusCode();

            AdminUserDto? updated = await updateResponse.Content.ReadFromJsonAsync<AdminUserDto>();
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Username, Is.EqualTo(expectedNormalized));
        }

        [TestCase("user_name", "user_name")]
        [TestCase("user.name", "user.name")]
        [TestCase("user-name", "user-name")]
        [TestCase("  MiXeD_Name.1  ", "mixed_name.1")]
        public async Task UpdateCurrentUser_WithValidUsernameSeparators_ReturnsSuccess(string username, string expectedNormalized)
        {
            string token = await LoginAsync();
            SetBearer(token);

            HttpResponseMessage updateResponse = await _client!.PutAsJsonAsync(
                "/api/v1/users/me",
                new
                {
                    Username = username
                });

            updateResponse.EnsureSuccessStatusCode();

            UserDto? updated = await updateResponse.Content.ReadFromJsonAsync<UserDto>();
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Username, Is.EqualTo(expectedNormalized));
        }

        [Test]
        public async Task UpdatePreferences_WithoutRealtimeToken_ReturnsUpdatedPreferences()
        {
            string token = await LoginAsync();
            SetBearer(token);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/users/me/preferences")
            {
                Content = JsonContent.Create(new Dictionary<string, string>
                {
                    ["cryptoEnvelope"] = "opaque-envelope"
                })
            };

            HttpResponseMessage response = await _client!.SendAsync(request);

            response.EnsureSuccessStatusCode();
            Dictionary<string, string>? preferences =
                await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            Assert.That(preferences, Is.Not.Null);
            Assert.That(preferences!["cryptoEnvelope"], Is.EqualTo("opaque-envelope"));
        }

        [Test]
        public async Task UpdatePreferences_RejectsMoreThan128PinnedFolders()
        {
            string token = await LoginAsync();
            SetBearer(token);
            string pinnedFolderIds = System.Text.Json.JsonSerializer.Serialize(
                Enumerable.Range(0, 129).Select(_ => Guid.NewGuid()));

            HttpResponseMessage response = await _client!.PatchAsJsonAsync(
                "/api/v1/users/me/preferences",
                new Dictionary<string, string>
                {
                    ["dashboardPinnedFolderIds"] = pinnedFolderIds,
                });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [TestCase("1bad")]
        [TestCase("a")]
        [TestCase("ab__cd")]
        public async Task Admin_UpdateUser_WithInvalidUsername_ReturnsBadRequest(string invalidUsername)
        {
            string token = await LoginAsync();
            SetBearer(token);

            UserDto created = await CreateUserAsync("targetuser", "target.user@example.com");

            HttpResponseMessage updateResponse = await _client!.PutAsJsonAsync(
                $"/api/v1/users/{created.Id}",
                new
                {
                    Username = invalidUsername,
                    Email = "target.user@example.com",
                    Role = UserRole.User,
                    FirstName = "Target",
                    LastName = "User",
                    BirthDate = new DateOnly(1995, 1, 1),
                    IsEmailVerified = false
                });

            Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Admin_UpdateUser_WithTakenUsername_ReturnsBadRequest()
        {
            string token = await LoginAsync();
            SetBearer(token);

            UserDto first = await CreateUserAsync("firstuser", "first.user@example.com");
            UserDto second = await CreateUserAsync("seconduser", "second.user@example.com");

            HttpResponseMessage updateResponse = await _client!.PutAsJsonAsync(
                $"/api/v1/users/{second.Id}",
                new
                {
                    Username = first.Username,
                    Email = "second.user@example.com",
                    Role = UserRole.User,
                    FirstName = "Second",
                    LastName = "User",
                    BirthDate = new DateOnly(1993, 7, 25),
                    IsEmailVerified = false
                });

            Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Admin_DeleteUser_DeletesAccountAndAllRelatedDatabaseRecords()
        {
            string adminToken = await LoginAsync();
            SetBearer(adminToken);
            UserDto created = await CreateUserAsync("deleteuser", "delete.user@example.com");
            string userToken = await LoginAsync("deleteuser", "UserPass_123");

            SetBearer(userToken);
            HttpResponseMessage authenticatedResponse = await _client!.GetAsync("/api/v1/users/me");
            authenticatedResponse.EnsureSuccessStatusCode();

            (
                Guid LayoutId,
                Guid RootNodeId,
                Guid ChildNodeId,
                Guid NodeFileId,
                Guid ManifestId,
                Guid ManifestChunkId,
                byte[] ChunkHash,
                Guid ProviderId) fixture = await SeedUserDeletionFixtureAsync(created.Id);

            SetBearer(adminToken);
            HttpResponseMessage deleteResponse = await _client!.DeleteAsync($"/api/v1/users/{created.Id}");

            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            await AssertUserDeletionFixtureRemovedAsync(created.Id, fixture);

            SetBearer(userToken);
            HttpResponseMessage revokedSessionResponse = await _client.GetAsync("/api/v1/users/me");
            Assert.That(revokedSessionResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Admin_DeleteUser_WhenDeletingOwnAccount_ReturnsBadRequest()
        {
            string adminToken = await LoginAsync();
            SetBearer(adminToken);

            UserDto? currentUser = await _client!.GetFromJsonAsync<UserDto>("/api/v1/users/me");
            Assert.That(currentUser, Is.Not.Null);

            HttpResponseMessage deleteResponse = await _client!.DeleteAsync($"/api/v1/users/{currentUser!.Id}");

            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            HttpResponseMessage currentUserResponse = await _client.GetAsync("/api/v1/users/me");
            currentUserResponse.EnsureSuccessStatusCode();
        }

        private async Task<UserDto> CreateUserAsync(string username, string email)
        {
            HttpResponseMessage createResponse = await _client!.PostAsJsonAsync(
                "/api/v1/users",
                new
                {
                    Username = username,
                    Email = email,
                    Password = "UserPass_123",
                    Role = UserRole.User
                });

            createResponse.EnsureSuccessStatusCode();

            UserDto? user = await createResponse.Content.ReadFromJsonAsync<UserDto>();
            Assert.That(user, Is.Not.Null);
            return user!;
        }

        private async Task<string> LoginAsync(
            string username = "testuser",
            string password = "testpassword")
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new CottonLoginRequestDto
                {
                    Username = username,
                    Password = password
                })
            };

            request.Headers.Add("X-Forwarded-For", "8.8.8.8");
            HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            TokenPairResponseDto? login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(login, Is.Not.Null);
            return login!.AccessToken;
        }

        private void SetBearer(string token)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private async Task<(
            Guid LayoutId,
            Guid RootNodeId,
            Guid ChildNodeId,
            Guid NodeFileId,
            Guid ManifestId,
            Guid ManifestChunkId,
            byte[] ChunkHash,
            Guid ProviderId)> SeedUserDeletionFixtureAsync(Guid userId)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            SettingsProvider settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider>();
            CottonServerSettings settings = await settingsProvider.EnsureServerSettingsAsync(null);

            Layout layout = new()
            {
                OwnerId = userId,
                IsActive = true,
            };
            Node root = new()
            {
                OwnerId = userId,
                Layout = layout,
                Type = NodeType.Default,
            };
            root.SetName("root");
            dbContext.UserLayouts.Add(layout);
            dbContext.Nodes.Add(root);
            await dbContext.SaveChangesAsync();

            Node child = new()
            {
                OwnerId = userId,
                LayoutId = layout.Id,
                Layout = layout,
                Type = NodeType.Default,
            };
            child.SetParent(root);
            child.SetName("documents");
            dbContext.Nodes.Add(child);
            await dbContext.SaveChangesAsync();

            byte[] chunkHash = Hasher.HashData(Encoding.UTF8.GetBytes("delete-user-chunk"));
            Chunk chunk = new()
            {
                Hash = chunkHash,
                PlainSizeBytes = 4,
                StoredSizeBytes = 4,
                CompressionAlgorithm = CompressionAlgorithm.Zstd,
            };
            FileManifest manifest = new()
            {
                ProposedContentHash = Hasher.HashData(Encoding.UTF8.GetBytes("delete-user-manifest")),
                ContentType = "text/plain",
                SizeBytes = 4,
            };
            FileManifestChunk manifestChunk = new()
            {
                FileManifest = manifest,
                Chunk = chunk,
                ChunkHash = chunkHash,
                ChunkOrder = 0,
            };
            NodeFile nodeFile = new()
            {
                OwnerId = userId,
                Node = child,
                FileManifest = manifest,
                OriginalNodeFileId = Guid.NewGuid(),
            };
            nodeFile.SetName("delete-me.txt");

            dbContext.Chunks.Add(chunk);
            dbContext.FileManifests.Add(manifest);
            dbContext.FileManifestChunks.Add(manifestChunk);
            dbContext.NodeFiles.Add(nodeFile);
            await dbContext.SaveChangesAsync();

            OidcProvider provider = new()
            {
                Name = "Deletion test provider",
                Slug = $"delete-{Guid.NewGuid():N}",
                Issuer = "https://issuer.example.com",
                ClientId = "client-id",
                Scopes = ["openid"],
                DefaultRole = UserRole.User,
            };

            dbContext.DownloadTokens.Add(new DownloadToken
            {
                FileName = nodeFile.Name,
                Token = Guid.NewGuid().ToString("N"),
                NodeFile = nodeFile,
                CreatedByUserId = userId,
            });
            dbContext.NodeShareTokens.Add(new NodeShareToken
            {
                Name = child.Name,
                Token = Guid.NewGuid().ToString("N"),
                Node = child,
                CreatedByUserId = userId,
            });
            dbContext.ChunkOwnerships.Add(new ChunkOwnership
            {
                OwnerId = userId,
                Chunk = chunk,
                ChunkHash = chunkHash,
            });
            dbContext.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Delete me",
                Priority = NotificationPriority.None,
            });
            dbContext.UserPasskeyCredentials.Add(new UserPasskeyCredential
            {
                UserId = userId,
                CredentialId = Hasher.HashData(Encoding.UTF8.GetBytes("credential")),
                PublicKey = [1, 2, 3],
                UserHandle = [4, 5, 6],
                Transports = ["internal"],
            });
            dbContext.OidcProviders.Add(provider);
            dbContext.UserExternalIdentities.Add(new UserExternalIdentity
            {
                UserId = userId,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = "delete-subject",
            });
            dbContext.OidcLoginStates.Add(new OidcLoginState
            {
                Provider = provider,
                StateHash = Guid.NewGuid().ToString("N"),
                CodeVerifierEncrypted = "verifier",
                NonceEncrypted = "nonce",
                ReturnUrl = "/",
                LinkUserId = userId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            });
            dbContext.SyncChanges.Add(new SyncChange
            {
                OwnerId = userId,
                Kind = Cotton.Models.Enums.SyncChangeKind.FileCreated,
                LayoutId = layout.Id,
                ItemId = nodeFile.Id,
                ParentNodeId = child.Id,
                FileManifestId = manifest.Id,
                Name = nodeFile.Name,
            });

            settings.DefaultUserTemplateNodeId = child.Id;

            await dbContext.SaveChangesAsync();
            return (
                layout.Id,
                root.Id,
                child.Id,
                nodeFile.Id,
                manifest.Id,
                manifestChunk.Id,
                chunkHash,
                provider.Id);
        }

        private async Task AssertUserDeletionFixtureRemovedAsync(
            Guid userId,
            (
                Guid LayoutId,
                Guid RootNodeId,
                Guid ChildNodeId,
                Guid NodeFileId,
                Guid ManifestId,
                Guid ManifestChunkId,
                byte[] ChunkHash,
                Guid ProviderId) fixture)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

            bool userExists = await dbContext.Users.AnyAsync(x => x.Id == userId);
            bool layoutExists = await dbContext.UserLayouts.AnyAsync(x => x.Id == fixture.LayoutId);
            bool nodeExists = await dbContext.Nodes.AnyAsync(
                x => x.Id == fixture.RootNodeId || x.Id == fixture.ChildNodeId);
            bool nodeFileExists = await dbContext.NodeFiles.AnyAsync(x => x.Id == fixture.NodeFileId);
            bool manifestExists = await dbContext.FileManifests.AnyAsync(x => x.Id == fixture.ManifestId);
            bool manifestChunkExists = await dbContext.FileManifestChunks.AnyAsync(
                x => x.Id == fixture.ManifestChunkId);
            bool chunkExists = await dbContext.Chunks.AnyAsync(x => x.Hash == fixture.ChunkHash);
            bool providerExists = await dbContext.OidcProviders.AnyAsync(x => x.Id == fixture.ProviderId);
            int downloadTokens = await dbContext.DownloadTokens.CountAsync(x => x.CreatedByUserId == userId);
            int shareTokens = await dbContext.NodeShareTokens.CountAsync(x => x.CreatedByUserId == userId);
            int chunkOwnerships = await dbContext.ChunkOwnerships.CountAsync(x => x.OwnerId == userId);
            int notifications = await dbContext.Notifications.CountAsync(x => x.UserId == userId);
            int passkeys = await dbContext.UserPasskeyCredentials.CountAsync(x => x.UserId == userId);
            int externalIdentities = await dbContext.UserExternalIdentities.CountAsync(x => x.UserId == userId);
            int oidcStates = await dbContext.OidcLoginStates.CountAsync(x => x.LinkUserId == userId);
            int refreshTokens = await dbContext.RefreshTokens.CountAsync(x => x.UserId == userId);
            int syncChanges = await dbContext.SyncChanges.CountAsync(x => x.OwnerId == userId);
            Guid? defaultTemplateNodeId = await dbContext.ServerSettings
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.DefaultUserTemplateNodeId)
                .FirstAsync();

            Assert.Multiple(() =>
            {
                Assert.That(userExists, Is.False);
                Assert.That(layoutExists, Is.False);
                Assert.That(nodeExists, Is.False);
                Assert.That(nodeFileExists, Is.False);
                Assert.That(manifestExists, Is.False);
                Assert.That(manifestChunkExists, Is.False);
                Assert.That(chunkExists, Is.True);
                Assert.That(providerExists, Is.True);
                Assert.That(downloadTokens, Is.Zero);
                Assert.That(shareTokens, Is.Zero);
                Assert.That(chunkOwnerships, Is.Zero);
                Assert.That(notifications, Is.Zero);
                Assert.That(passkeys, Is.Zero);
                Assert.That(externalIdentities, Is.Zero);
                Assert.That(oidcStates, Is.Zero);
                Assert.That(refreshTokens, Is.Zero);
                Assert.That(syncChanges, Is.Zero);
                Assert.That(defaultTemplateNodeId, Is.Null);
            });
        }
    }
}
