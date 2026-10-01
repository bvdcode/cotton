// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;

namespace Cotton.Server.IntegrationTests
{
    public class UserDeletionTests : UserManagementTestBase
    {
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
