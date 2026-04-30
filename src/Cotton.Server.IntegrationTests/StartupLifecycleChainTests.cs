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
using System.Text.Json;

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
    public async Task FirstSettingsPatch_CreatesSafeDefaults_AndSetsSetupCompleteFlag()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        bool before = await GetIsServerInitializedAsync();
        Assert.That(before, Is.False);

        HttpResponseMessage response = await _client!.PatchAsJsonAsync(
            "/api/v1/server/settings/telemetry",
            false);
        response.EnsureSuccessStatusCode();

        bool after = await GetIsServerInitializedAsync();
        Assert.That(after, Is.True);

        JsonElement publicBaseUrl = await GetJsonAsync("/api/v1/server/settings/public-base-url");
        Assert.That(publicBaseUrl.GetProperty("publicBaseUrl").GetString(), Does.Contain("localhost"));
    }

    [Test]
    public async Task SettingsPatchFlow_PersistsIndependentConfigurationSteps()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        (await _client!.PatchAsJsonAsync("/api/v1/server/settings/allow-cross-user-deduplication", true)).EnsureSuccessStatusCode();
        (await _client!.PatchAsJsonAsync("/api/v1/server/settings/allow-global-indexing", true)).EnsureSuccessStatusCode();
        (await _client!.PatchAsJsonAsync("/api/v1/server/settings/server-usage", new[] { "Photos", "Documents" })).EnsureSuccessStatusCode();
        (await _client!.PatchAsJsonAsync("/api/v1/server/settings/telemetry", true)).EnsureSuccessStatusCode();
        (await _client!.PatchAsync("/api/v1/server/settings/compution-mode/Local", null)).EnsureSuccessStatusCode();
        (await _client!.PatchAsJsonAsync("/api/v1/server/settings/timezone", "UTC")).EnsureSuccessStatusCode();
        (await _client!.PatchAsync("/api/v1/server/settings/storage-space-mode/Limited", null)).EnsureSuccessStatusCode();
        (await _client!.PatchAsJsonAsync("/api/v1/server/settings/public-base-url", "https://cotton.example/")).EnsureSuccessStatusCode();
        (await _client!.PatchAsJsonAsync("/api/v1/server/settings/custom-geoip-lookup-url", "https://geo.example/lookup/{ip}")).EnsureSuccessStatusCode();
        (await _client!.PatchAsync("/api/v1/server/settings/geoip-lookup-mode/CustomHttp", null)).EnsureSuccessStatusCode();

        var emailConfig = new EmailConfig
        {
            SmtpServer = "smtp.example.com",
            Port = "587",
            Username = "mailer",
            Password = "secret",
            FromAddress = "noreply@example.com",
            UseSSL = true
        };
        (await _client!.PatchAsJsonAsync("/api/v1/server/settings/email-config", emailConfig)).EnsureSuccessStatusCode();
        (await _client!.PatchAsync("/api/v1/server/settings/email-mode/Custom", null)).EnsureSuccessStatusCode();

        Assert.That(await GetIsServerInitializedAsync(), Is.True);

        JsonElement publicBaseUrl = await GetJsonAsync("/api/v1/server/settings/public-base-url");
        JsonElement serverUsage = await GetJsonAsync("/api/v1/server/settings/server-usage");
        JsonElement geoIpMode = await GetJsonAsync("/api/v1/server/settings/geoip-lookup-mode");
        JsonElement emailMode = await GetJsonAsync("/api/v1/server/settings/email-mode");
        JsonElement storedEmailConfig = await GetJsonAsync("/api/v1/server/settings/email-config");

        Assert.Multiple(() =>
        {
            Assert.That(publicBaseUrl.GetProperty("publicBaseUrl").GetString(), Is.EqualTo("https://cotton.example"));
            Assert.That(serverUsage.GetProperty("serverUsage").EnumerateArray().Select(x => x.GetString()), Does.Contain("Photos"));
            Assert.That(geoIpMode.GetProperty("geoIpLookupMode").GetString(), Is.EqualTo("CustomHttp"));
            Assert.That(emailMode.GetProperty("emailMode").GetString(), Is.EqualTo("Custom"));
            Assert.That(storedEmailConfig.GetProperty("smtpServer").GetString(), Is.EqualTo("smtp.example.com"));
            Assert.That(storedEmailConfig.GetProperty("password").GetString(), Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public async Task SettingsPatch_Rejects_InvalidTimezone_ButKeepsSafeDefaults()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        bool before = await GetIsServerInitializedAsync();
        Assert.That(before, Is.False);

        var response = await _client!.PatchAsJsonAsync(
            "/api/v1/server/settings/timezone",
            "Mars/OlympusMons");

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings/timezone");
        Assert.That(await GetIsServerInitializedAsync(), Is.True);
    }

    [Test]
    public async Task SettingsPatch_Rejects_CloudEmail_WithoutTelemetry()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PatchAsync("/api/v1/server/settings/email-mode/Cloud", null);

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings/email-mode/Cloud");
    }

    [Test]
    public async Task SettingsPatch_Rejects_CloudComputation_WithoutTelemetry()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PatchAsync("/api/v1/server/settings/compution-mode/Cloud", null);

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings/compution-mode/Cloud");
    }

    [Test]
    public async Task SettingsPatch_Rejects_CustomEmail_WithoutEmailConfig()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PatchAsync("/api/v1/server/settings/email-mode/Custom", null);

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings/email-mode/Custom");
    }

    [Test]
    public async Task SettingsPatch_Rejects_S3Storage_WithoutS3Config()
    {
        TokenPairResponseDto login = await LoginAsync();
        SetBearer(login.AccessToken);

        var response = await _client!.PatchAsync("/api/v1/server/settings/storage-type/S3", null);

        await AssertBadRequestProblemDetailsAsync(response, "/api/v1/server/settings/storage-type/S3");
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

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        EnsureClientCreated();

        JsonElement response = await _client!.GetFromJsonAsync<JsonElement>(url);
        return response;
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
