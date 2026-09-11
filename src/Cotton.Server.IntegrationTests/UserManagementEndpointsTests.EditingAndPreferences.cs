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

    }
}
