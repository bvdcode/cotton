// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Reflection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests;

public class ServerEndpointsTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        ResetSettingsProviderCaches();

        IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
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
            ["EncryptionThreads"] = "2",
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
        ResetSettingsProviderCaches();
    }

    [Test]
    public async Task Get_Settings_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage res = await _client!.GetAsync("/api/v1/server/settings");
        res.EnsureSuccessStatusCode();
        Dictionary<string, object>? settings = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings!.ContainsKey("maxChunkSizeBytes"), Is.True);
        Assert.That(settings!.ContainsKey("supportedHashAlgorithm"), Is.True);
    }

    [Test]
    public async Task TrustedProxyAddress_IsSavedOnlyWhenItMatchesCurrentConnection()
    {
        string token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add(TestAppFactory.RemoteIpAddressHeader, "198.51.100.10");

        JsonElement observedPayload = await _client.GetFromJsonAsync<JsonElement>(
            "/api/v1/server/settings/trusted-proxy-ip-address/observed");
        string? observedAddress = observedPayload.GetProperty("observedProxyIpAddress").GetString();
        Assert.That(observedAddress, Is.Not.Null.And.Not.Empty);

        using HttpResponseMessage mismatchResponse = await _client.PostAsJsonAsync(
            "/api/v1/server/settings/trusted-proxy-ip-address/verify-and-save",
            "192.0.2.250");
        mismatchResponse.EnsureSuccessStatusCode();
        JsonElement mismatchPayload = await mismatchResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(mismatchPayload.GetProperty("matches").GetBoolean(), Is.False);
            Assert.That(mismatchPayload.GetProperty("saved").GetBoolean(), Is.False);
            Assert.That(mismatchPayload.GetProperty("observedProxyIpAddress").GetString(), Is.EqualTo(observedAddress));
        });

        JsonElement unsetPayload = await _client.GetFromJsonAsync<JsonElement>(
            "/api/v1/server/settings/trusted-proxy-ip-address");
        Assert.That(unsetPayload.GetProperty("trustedProxyIpAddress").ValueKind, Is.EqualTo(JsonValueKind.Null));

        using HttpResponseMessage savedResponse = await _client.PostAsJsonAsync(
            "/api/v1/server/settings/trusted-proxy-ip-address/verify-and-save",
            observedAddress);
        savedResponse.EnsureSuccessStatusCode();
        JsonElement savedPayload = await savedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(savedPayload.GetProperty("matches").GetBoolean(), Is.True);
            Assert.That(savedPayload.GetProperty("saved").GetBoolean(), Is.True);
            Assert.That(savedPayload.GetProperty("trustedProxyIpAddress").GetString(), Is.EqualTo(observedAddress));
        });

        JsonElement configuredPayload = await _client.GetFromJsonAsync<JsonElement>(
            "/api/v1/server/settings/trusted-proxy-ip-address");
        Assert.That(
            configuredPayload.GetProperty("trustedProxyIpAddress").GetString(),
            Is.EqualTo(observedAddress));
    }

    [Test]
    public async Task Begin_Passkey_Registration_Requests_Direct_Attestation()
    {
        string token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/auth/passkeys/registration/options",
            new { Label = "Office key" });
        response.EnsureSuccessStatusCode();

        JsonElement payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(payload.GetProperty("options").GetProperty("attestation").GetString(), Is.EqualTo("direct"));
    }

    [Test]
    public async Task Set_Passkey_Label_With_Blank_Value_Clears_Label_AndKeepsDetectedKind()
    {
        string token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Guid credentialId = await CreatePasskeyCredentialAsync("Desk key", Guid.Empty, ["usb", "nfc"]);

        using HttpResponseMessage response = await _client.PutAsJsonAsync(
            $"/api/v1/auth/passkeys/{credentialId}",
            new { Label = "   " });
        response.EnsureSuccessStatusCode();

        JsonElement payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("label").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(payload.GetProperty("authenticatorName").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(payload.GetProperty("authenticatorKind").GetString(), Is.EqualTo("SecurityKey"));
        });
    }

    [Test]
    public async Task Set_Chunk_Size_IsAdminOnly_AndPersists()
    {
        using HttpResponseMessage unauthenticatedResponse = await _client!.PatchAsync(
            "/api/v1/server/settings/chunk-size/16777216",
            null);
        Assert.That(unauthenticatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage setResponse = await _client.PatchAsync(
            "/api/v1/server/settings/chunk-size/16777216",
            null);
        setResponse.EnsureSuccessStatusCode();
        JsonElement setPayload = await setResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(setPayload.GetProperty("maxChunkSizeBytes").GetInt32(), Is.EqualTo(16 * 1024 * 1024));
        Assert.That(setPayload.GetProperty("supportedMaxChunkSizeBytes").GetArrayLength(), Is.EqualTo(3));

        JsonElement getPayload = await _client.GetFromJsonAsync<JsonElement>("/api/v1/server/settings/chunk-size");
        Assert.That(getPayload.GetProperty("maxChunkSizeBytes").GetInt32(), Is.EqualTo(16 * 1024 * 1024));

        using HttpResponseMessage invalidResponse = await _client.PatchAsync(
            "/api/v1/server/settings/chunk-size/33554432",
            null);
        Assert.That(invalidResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Set_Storage_Pipeline_Settings_AreAdminOnly_AndPersist()
    {
        using HttpResponseMessage unauthenticatedResponse = await _client!.PatchAsync(
            "/api/v1/server/settings/compression-level/1",
            null);
        Assert.That(unauthenticatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage compressionResponse = await _client.PatchAsync(
            "/api/v1/server/settings/compression-level/1",
            null);
        compressionResponse.EnsureSuccessStatusCode();
        JsonElement compressionPayload = await compressionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(compressionPayload.GetProperty("compressionLevel").GetInt32(), Is.EqualTo(1));
        Assert.That(compressionPayload.GetProperty("minCompressionLevel").GetInt32(), Is.LessThanOrEqualTo(1));
        Assert.That(compressionPayload.GetProperty("maxCompressionLevel").GetInt32(), Is.GreaterThanOrEqualTo(1));

        using HttpResponseMessage cipherResponse = await _client.PatchAsync(
            "/api/v1/server/settings/cipher-chunk-size/4194304",
            null);
        cipherResponse.EnsureSuccessStatusCode();
        JsonElement cipherPayload = await cipherResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(cipherPayload.GetProperty("cipherChunkSizeBytes").GetInt32(), Is.EqualTo(4 * 1024 * 1024));

        using HttpResponseMessage threadsResponse = await _client.PatchAsync(
            "/api/v1/server/settings/encryption-threads/1",
            null);
        threadsResponse.EnsureSuccessStatusCode();
        JsonElement threadsPayload = await threadsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(threadsPayload.GetProperty("encryptionThreads").GetInt32(), Is.EqualTo(1));
        Assert.That(threadsPayload.GetProperty("supportedEncryptionThreads").GetArrayLength(), Is.GreaterThanOrEqualTo(1));

        JsonElement getPayload = await _client.GetFromJsonAsync<JsonElement>("/api/v1/server/settings/storage-pipeline");
        Assert.That(getPayload.GetProperty("compressionLevel").GetInt32(), Is.EqualTo(1));
        Assert.That(getPayload.GetProperty("cipherChunkSizeBytes").GetInt32(), Is.EqualTo(4 * 1024 * 1024));
        Assert.That(getPayload.GetProperty("encryptionThreads").GetInt32(), Is.EqualTo(1));
        Assert.That(GetResolvedEncryptionThreads(), Is.EqualTo(1));
    }

    [Test]
    public async Task Get_CurrentUser_Works()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        UserDto? me = await _client.GetFromJsonAsync<UserDto>("/api/v1/users/me");
        Assert.That(me, Is.Not.Null);
        Assert.That(me!.Username, Is.EqualTo("testuser"));
    }

    [Test]
    public async Task Get_SecurityStatus_IsAdminOnly_AndReturnsDiagnostics()
    {
        HttpResponseMessage unauthenticatedResponse = await _client!.GetAsync("/api/v1/server/security/status");
        Assert.That(unauthenticatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var token = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/server/security/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();

        JsonElement payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(payload.TryGetProperty("dotNetDiagnostics", out _), Is.True);
            Assert.That(payload.TryGetProperty("linuxProcess", out _), Is.True);
            Assert.That(payload.TryGetProperty("linuxContainer", out JsonElement linuxContainer), Is.True);
            Assert.That(payload.TryGetProperty("warnings", out JsonElement warnings), Is.True);
            Assert.That(payload.TryGetProperty("securityScore", out JsonElement score), Is.True);
            Assert.That(payload.TryGetProperty("maxSecurityScore", out JsonElement maxScore), Is.True);
            Assert.That(payload.TryGetProperty("isPublicInstance", out _), Is.True);
            Assert.That(payload.TryGetProperty("trustedProxyIpAddress", out JsonElement trustedProxy), Is.True);
            Assert.That(payload.TryGetProperty("adminTotp", out JsonElement adminTotp), Is.True);
            Assert.That(warnings.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(score.GetInt32(), Is.InRange(0, 10));
            Assert.That(maxScore.GetInt32(), Is.EqualTo(10));
            Assert.That(trustedProxy.ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(payload.GetProperty("masterKeySource").GetString(), Is.Not.Empty);
            Assert.That(adminTotp.GetProperty("adminCount").GetInt32(), Is.EqualTo(1));
            Assert.That(adminTotp.GetProperty("adminsWithTotp").GetInt32(), Is.EqualTo(0));
            Assert.That(adminTotp.GetProperty("adminsWithoutTotp").GetInt32(), Is.EqualTo(1));
            Assert.That(linuxContainer.TryGetProperty("rootFilesystemReadOnly", out _), Is.True);
            Assert.That(linuxContainer.TryGetProperty("dockerSocketMounted", out _), Is.True);
            Assert.That(linuxContainer.TryGetProperty("hostPidNamespaceLikely", out _), Is.True);
            Assert.That(linuxContainer.TryGetProperty("coreDumpSoftLimit", out _), Is.True);
            Assert.That(linuxContainer.TryGetProperty("corePattern", out _), Is.True);
            Assert.That(linuxContainer.TryGetProperty("appArmorProfile", out _), Is.True);
            Assert.That(linuxContainer.TryGetProperty("selinuxContext", out _), Is.True);
            Assert.That(
                warnings.EnumerateArray().Any(warning =>
                    warning.GetProperty("code").GetString() == "admins-without-2fa"),
                Is.True);
            Assert.That(
                warnings.EnumerateArray().Any(warning =>
                    warning.GetProperty("code").GetString() == "trusted-proxy-not-configured"
                    && warning.GetProperty("severity").GetString() == "warning"),
                Is.True);
        });
    }

    [Test]
    public async Task Get_LatestDatabaseBackup_ReturnsNotFound_WhenNoBackupExists()
    {
        var token = await LoginAsync();
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.GetAsync("/api/v1/server/database-backup/latest");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.NotFound));
    }

    private static void ResetSettingsProviderCaches()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type settingsProviderType = typeof(SettingsProvider);

        settingsProviderType.GetField("_cache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_cachedEncryptionThreads", flags)?.SetValue(null, 0);
        settingsProviderType.GetField("_isServerInitializedCache", flags)?.SetValue(null, null);
        settingsProviderType.GetField("_serverHasUsersCache", flags)?.SetValue(null, null);
    }

    private int GetResolvedEncryptionThreads()
    {
        using IServiceScope scope = _factory!.Services.CreateScope();
        IStreamCipher cipher = scope.ServiceProvider.GetRequiredService<IStreamCipher>();
        Assert.That(cipher, Is.TypeOf<AesGcmStreamCipher>());

        return ((AesGcmStreamCipher)cipher).ConcurrencyLevel;
    }

    private async Task<Guid> CreatePasskeyCredentialAsync(
        string? label,
        Guid aaGuid,
        string[] transports)
    {
        using IServiceScope scope = _factory!.Services.CreateScope();
        CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
        User user = await dbContext.Users.SingleAsync(x => x.Username == "testuser");
        UserPasskeyCredential credential = new()
        {
            UserId = user.Id,
            CredentialId = Guid.NewGuid().ToByteArray(),
            PublicKey = [1, 2, 3],
            UserHandle = user.Id.ToByteArray(),
            SignatureCounter = 0,
            Label = label,
            Transports = transports,
            AaGuid = aaGuid,
            IsBackupEligible = false,
            IsBackedUp = false
        };

        await dbContext.UserPasskeyCredentials.AddAsync(credential);
        await dbContext.SaveChangesAsync();
        return credential.Id;
    }

    private async Task<string> LoginAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new CottonLoginRequestDto()
            {
                Username = "testuser",
                Password = "testpassword"
            })
        };
        request.Headers.Add("X-Forwarded-For", "8.8.8.8");
        HttpResponseMessage res = await _client!.SendAsync(request);
        res.EnsureSuccessStatusCode();
        TokenPairResponseDto? login = await res.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);
        return login!.AccessToken;
    }
}
