// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using Cotton.Auth;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class UserManagementEndpointsTests
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

    }
}
