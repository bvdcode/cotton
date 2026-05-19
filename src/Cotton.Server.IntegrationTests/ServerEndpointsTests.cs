// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
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
using System.Text.Json;

namespace Cotton.Server.IntegrationTests;

public class ServerEndpointsTests : IntegrationTestBase
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
    public async Task Get_Settings_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client!.GetAsync("/api/v1/server/settings");
        res.EnsureSuccessStatusCode();
        var settings = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings!.ContainsKey("maxChunkSizeBytes"), Is.True);
        Assert.That(settings!.ContainsKey("supportedHashAlgorithm"), Is.True);
    }

    [Test]
    public async Task Get_CurrentUser_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await _client.GetFromJsonAsync<Models.Dto.UserDto>("/api/v1/users/me");
        Assert.That(me, Is.Not.Null);
        Assert.That(me!.Username, Is.EqualTo("testuser"));
    }

    [Test]
    public async Task Get_SecurityStatus_IsAdminOnly_AndReturnsDiagnostics()
    {
        var unauthenticatedResponse = await _client!.GetAsync("/api/v1/server/security/status");
        Assert.That(unauthenticatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/server/security/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();

        JsonElement payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.TryGetProperty("dotNetDiagnostics", out _), Is.True);
            Assert.That(payload.TryGetProperty("linuxProcess", out _), Is.True);
            Assert.That(payload.TryGetProperty("warnings", out JsonElement warnings), Is.True);
            Assert.That(payload.TryGetProperty("securityScore", out JsonElement score), Is.True);
            Assert.That(payload.TryGetProperty("maxSecurityScore", out JsonElement maxScore), Is.True);
            Assert.That(payload.TryGetProperty("isPublicInstance", out _), Is.True);
            Assert.That(payload.TryGetProperty("adminTotp", out JsonElement adminTotp), Is.True);
            Assert.That(warnings.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(score.GetInt32(), Is.InRange(0, 10));
            Assert.That(maxScore.GetInt32(), Is.EqualTo(10));
            Assert.That(payload.GetProperty("masterKeySource").GetString(), Is.Not.Empty);
            Assert.That(adminTotp.GetProperty("adminCount").GetInt32(), Is.EqualTo(1));
            Assert.That(adminTotp.GetProperty("adminsWithTotp").GetInt32(), Is.EqualTo(0));
            Assert.That(adminTotp.GetProperty("adminsWithoutTotp").GetInt32(), Is.EqualTo(1));
            Assert.That(
                warnings.EnumerateArray().Any(warning =>
                    warning.GetProperty("code").GetString() == "admins-without-2fa"),
                Is.True);
        });
    }

    [Test]
    public async Task Get_LatestDatabaseBackup_ReturnsNotFound_WhenNoBackupExists()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/server/database-backup/latest");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.NotFound));
    }

    private async Task<string> LoginAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequestDto()
            {
                Username = "testuser",
                Password = "testpassword"
            })
        };
        request.Headers.Add("X-Forwarded-For", "8.8.8.8");
        var res = await _client!.SendAsync(request);
        res.EnsureSuccessStatusCode();
        var login = await res.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }
}
