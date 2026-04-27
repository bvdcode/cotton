// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
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
using System.Reflection;
using System.Text;

namespace Cotton.Server.IntegrationTests;

public class StartupLifecycleChainTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
    private HttpClient? _client;

    private sealed record IsServerInitializedResponse(bool IsServerInitialized);
    private sealed record ProblemDetailsResponse(string? Type, string? Title, int? Status, string? Detail, string? Instance);

    [SetUp]
    public void SetUp()
    {
        _client = null;
        _factory = null;

        ResetSettingsProviderCaches();

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
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();

        _client = null;
        _factory = null;
    }

    [Test]
    public async Task Startup_OnCleanDatabase_AppliesMigrations_AndCreatesInitialAdminWithinWindow()
    {
        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
        Assert.That(creator.HasTables(), Is.False, "DB should start with no user tables in this test setup.");

        TokenPairResponseDto login = await LoginAsync();
        Assert.That(login.AccessToken, Is.Not.Null.And.Not.Empty);

        Assert.That(creator.HasTables(), Is.True, "Server startup should apply migrations automatically.");

        SetBearer(login.AccessToken);
        UserDto? me = await _client!.GetFromJsonAsync<UserDto>("/api/v1/users/me");
        Assert.That(me, Is.Not.Null);
        Assert.That(me!.Username, Is.EqualTo("testuser"));
        Assert.That(me.Role, Is.EqualTo(UserRole.Admin), "First user should be admin on non-public instance.");
    }

    [Test]
    public async Task Login_ForUnknownUser_AfterAdminExists_ReturnsUnauthorized()
    {
        await LoginAsync();

        HttpResponseMessage secondLogin = await LoginRawAsync("new-user", "some-password");
        Assert.That(secondLogin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task InitialSettings_SetupFlag_Transitions_FromFalse_ToTrue()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        bool before = await GetIsServerInitializedAsync();
        Assert.That(before, Is.False);

        var createResponse = await _client!.PostAsJsonAsync(
            "/api/v1/server/settings",
            CreateValidInitialSettings());
        createResponse.EnsureSuccessStatusCode();

        bool after = await GetIsServerInitializedAsync();
        Assert.That(after, Is.True);
    }

    [Test]
    public async Task CreateInitialSettings_Rejects_InvalidTimezone()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PostAsJsonAsync(
            "/api/v1/server/settings",
            CreateValidInitialSettings(timezone: "Mars/OlympusMons"));

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings");
    }

    [Test]
    public async Task CreateInitialSettings_Rejects_CloudEmail_WithoutTelemetry()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PostAsJsonAsync(
            "/api/v1/server/settings",
            CreateValidInitialSettings(
                telemetry: false,
                email: EmailMode.Cloud));

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings");
    }

    [Test]
    public async Task CreateInitialSettings_Rejects_CloudComputation_WithoutTelemetry()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PostAsJsonAsync(
            "/api/v1/server/settings",
            CreateValidInitialSettings(
                telemetry: false,
                computionMode: ComputionMode.Cloud));

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings");
    }

    [Test]
    public async Task CreateInitialSettings_Rejects_CustomEmail_WithoutEmailConfig()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PostAsJsonAsync(
            "/api/v1/server/settings",
            CreateValidInitialSettings(
                email: EmailMode.Custom,
                emailConfig: null));

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings");
    }

    [Test]
    public async Task CreateInitialSettings_Rejects_S3Storage_WithoutS3Config()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PostAsJsonAsync(
            "/api/v1/server/settings",
            CreateValidInitialSettings(
                storage: StorageType.S3,
                s3Config: null));

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings");
    }

    [Test]
    public async Task CreateInitialSettings_InvalidRequest_DoesNotSetSetupCompleteFlag()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        bool before = await GetIsServerInitializedAsync();
        Assert.That(before, Is.False);

        var response = await _client!.PostAsJsonAsync(
            "/api/v1/server/settings",
            CreateValidInitialSettings(timezone: "Mars/OlympusMons"));

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings");

        bool after = await GetIsServerInitializedAsync();
        Assert.That(after, Is.False, "Invalid setup requests must not mark server as initialized.");
    }

    [Test]
    public async Task CreateInitialSettings_WithFallbackPublicBaseUrl_PersistsTrimmedUrl()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PostAsJsonAsync(
            "/api/v1/server/settings",
            CreateValidInitialSettings(publicBaseUrl: " "));

        response.EnsureSuccessStatusCode();

        var envelope = await _client!.GetFromJsonAsync<ServerSettingsEnvelopeDto>("/api/v1/server/settings");
        Assert.That(envelope, Is.Not.Null);
        Assert.That(envelope!.Settings, Is.Not.Null);
        Assert.That(envelope.Settings!.PublicBaseUrl, Does.Contain("localhost"));
        Assert.That(envelope.Settings.PublicBaseUrl.EndsWith('/'), Is.False);
        Assert.That(envelope.Settings.StorageType, Is.EqualTo(StorageType.Local));
        Assert.That(envelope.Settings.EmailMode, Is.EqualTo(EmailMode.None));
    }

    private static CottonServerSettingsDto CreateValidInitialSettings(
        string timezone = "UTC",
        bool telemetry = false,
        EmailMode email = EmailMode.None,
        StorageType storage = StorageType.Local,
        ComputionMode computionMode = ComputionMode.Local,
        StorageSpaceMode storageSpace = StorageSpaceMode.Optimal,
        string? publicBaseUrl = "http://localhost",
        S3Config? s3Config = null,
        EmailConfig? emailConfig = null)
    {
        return new CottonServerSettingsDto
        {
            TrustedMode = false,
            Usage = [ServerUsage.Other],
            Telemetry = telemetry,
            Storage = storage,
            Email = email,
            ComputionMode = computionMode,
            Timezone = timezone,
            StorageSpace = storageSpace,
            PublicBaseUrl = publicBaseUrl,
            S3Config = s3Config,
            EmailConfig = emailConfig
        };
    }

    private async Task<TokenPairResponseDto> LoginAsync(string username = "testuser", string password = "testpassword")
    {
        EnsureClientCreated();

        HttpResponseMessage response = await LoginRawAsync(username, password);
        response.EnsureSuccessStatusCode();

        TokenPairResponseDto? payload = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(payload, Is.Not.Null);
        return payload!;
    }

    private async Task<HttpResponseMessage> LoginRawAsync(string username, string password)
    {
        EnsureClientCreated();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequestDto
            {
                Username = username,
                Password = password
            })
        };

        request.Headers.Add("X-Forwarded-For", "8.8.8.8");
        return await _client!.SendAsync(request);
    }

    private async Task<bool> GetIsServerInitializedAsync()
    {
        EnsureClientCreated();

        var response = await _client!.GetFromJsonAsync<IsServerInitializedResponse>("/api/v1/server/settings/is-setup-complete");
        Assert.That(response, Is.Not.Null);
        return response!.IsServerInitialized;
    }

    private void SetBearer(string accessToken)
    {
        EnsureClientCreated();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task AssertBadRequestProblemDetailsAsync(HttpResponseMessage response, string expectedInstance)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        ProblemDetailsResponse? payload = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Status, Is.EqualTo((int)HttpStatusCode.BadRequest));
        Assert.That(payload.Title, Is.EqualTo("Bad Request"));
        Assert.That(payload.Detail, Is.EqualTo("Bad request"));
        Assert.That(payload.Instance, Is.EqualTo(expectedInstance));
    }

    private void EnsureClientCreated()
    {
        if (_client is not null)
        {
            return;
        }

        _client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private static void ResetSettingsProviderCaches()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        Type settingsProviderType = typeof(SettingsProvider);

        settingsProviderType.GetField("_cache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_isServerInitializedCache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_serverHasUsersCache", flags)?.SetValue(null, null);
    }
}
