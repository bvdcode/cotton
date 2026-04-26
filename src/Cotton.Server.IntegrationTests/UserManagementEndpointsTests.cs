// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Cotton.Server.IntegrationTests;

public class UserManagementEndpointsTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        creator.Create();
        Assert.Multiple(() =>
        {
            Assert.That(creator.Exists(), Is.True);
            Assert.That(creator.HasTables(), Is.False);
        });

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = DatabaseName,
            Username = "postgres",
            Password = "postgres"
        };
        var overrides = new Dictionary<string, string?>
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

        var createResponse = await _client!.PostAsJsonAsync(
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
            Assert.That(created.Role, Is.EqualTo(UserRole.User));
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

        var createResponse = await _client!.PostAsJsonAsync(
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

        var createResponse = await _client!.PostAsJsonAsync(
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

        var updateResponse = await _client!.PutAsJsonAsync(
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
    public async Task Admin_UpdateUser_WithValidUsername_ReturnsSuccess(string validUsername)
    {
        string token = await LoginAsync();
        SetBearer(token);

        UserDto created = await CreateUserAsync("updatebase", "update.base@example.com");

        var updateResponse = await _client!.PutAsJsonAsync(
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
    public async Task UpdateCurrentUser_WithValidUsernameSeparators_ReturnsSuccess(string username, string expectedNormalized)
    {
        string token = await LoginAsync();
        SetBearer(token);

        var updateResponse = await _client!.PutAsJsonAsync(
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

    [TestCase("1bad")]
    [TestCase("a")]
    [TestCase("ab__cd")]
    public async Task Admin_UpdateUser_WithInvalidUsername_ReturnsBadRequest(string invalidUsername)
    {
        string token = await LoginAsync();
        SetBearer(token);

        UserDto created = await CreateUserAsync("targetuser", "target.user@example.com");

        var updateResponse = await _client!.PutAsJsonAsync(
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

        var updateResponse = await _client!.PutAsJsonAsync(
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

    private async Task<UserDto> CreateUserAsync(string username, string email)
    {
        var createResponse = await _client!.PostAsJsonAsync(
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

    private async Task<string> LoginAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequestDto
            {
                Username = "testuser",
                Password = "testpassword"
            })
        };

        request.Headers.Add("X-Forwarded-For", "8.8.8.8");
        var response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();

        TokenPairResponseDto? login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }

    private void SetBearer(string token)
    {
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}