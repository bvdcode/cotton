// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using ServerChangePasswordRequestDto = Cotton.Server.Models.Requests.ChangePasswordRequestDto;
using Cotton.Storage.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests;

public class AuthSmokeTests : IntegrationTestBase
{
    private TestAppFactory? _factory;
    private WebApplicationFactory<Program>? _customFactory;
    private HttpClient? _client;
    private RecordingNotificationsProvider? _notifications;

    [SetUp]
    public void SetUp()
    {
        IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        creator.Create();
        Assert.Multiple(() =>
        {
            Assert.That(creator.Exists(), Is.True, "DB must exist after Create()");
            Assert.That(creator.HasTables(), Is.False, "DB must have no user tables after Create()");
        });

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = TestPostgresHost,
            Port = TestPostgresPort,
            Database = CurrentDatabaseName,
            Username = TestPostgresUsername,
            Password = TestPostgresPassword
        };

        var overrides = new Dictionary<string, string?>
        {
            ["DatabaseSettings:Host"] = csb.Host,
            ["DatabaseSettings:Port"] = csb.Port.ToString(),
            ["DatabaseSettings:Database"] = csb.Database,
            ["DatabaseSettings:Username"] = csb.Username,
            ["DatabaseSettings:Password"] = csb.Password,
            ["MasterEncryptionKey"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("0123456789ABCDEF0123456789ABCDEF")),
            ["MasterEncryptionKeyId"] = "1",
            ["EncryptionThreads"] = "1",
            ["MaxChunkSizeBytes"] = "16777216",
            ["CipherChunkSizeBytes"] = "20971520",
            ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4"
        };

        _factory = new TestAppFactory(overrides);
        RecordingNotificationsProvider notifications = new();
        _notifications = notifications;
        _customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                ServiceDescriptor? existing = services.FirstOrDefault(d => d.ServiceType == typeof(IStoragePipeline));
                if (existing != null) services.Remove(existing);
                services.AddSingleton<IStoragePipeline, InMemoryStorage>();

                ServiceDescriptor? existingNotifications = services
                    .FirstOrDefault(d => d.ServiceType == typeof(INotificationsProvider));
                if (existingNotifications is not null)
                {
                    services.Remove(existingNotifications);
                }
                services.AddSingleton<INotificationsProvider>(notifications);
            });
            builder.ConfigureLogging((ctx, logging) =>
            {
                logging.ClearProviders();
                logging.AddProvider(new NUnitLoggerProvider());
                logging.SetMinimumLevel(LogLevel.Information);
            });
        });

        _client = _customFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _customFactory?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Login_Returns_Token()
    {
        Assert.That(_client, Is.Not.Null);
        Assert.That(_notifications, Is.Not.Null);

        TokenPairResponseDto payload = await LoginAsync("testuser", "testpassword");
        Assert.Multiple(() =>
        {
            Assert.That(string.IsNullOrWhiteSpace(payload.AccessToken), Is.False, "Token must be present");
            Assert.That(_notifications!.Emails, Has.Count.EqualTo(1));
        });

        var (_, template, parameters, _, _) = _notifications!.Emails.Single();
        Assert.Multiple(() =>
        {
            Assert.That(template, Is.EqualTo(EmailTemplate.SecurityAlert));
            Assert.That(parameters["security_title"], Is.EqualTo("New login to your account"));
            Assert.That(parameters["security_content"], Does.Contain("8.8.8.8"));
        });

        var parts = payload.AccessToken.Split('.');
        Assert.That(parts.Length, Is.EqualTo(3), "JWT must have3 parts");

        TestContext.Progress.WriteLine($"Login OK. Token: {payload.AccessToken[..Math.Min(16, payload.AccessToken.Length)]}...");
    }

    [Test]
    public async Task Login_Succeeds_WhenSecurityEmailFails()
    {
        RecordingNotificationsProvider notifications = _notifications
            ?? throw new InvalidOperationException("Notifications provider is not configured.");
        notifications.ThrowOnEmail = true;

        TokenPairResponseDto payload = await LoginAsync("emailfailure", "testpassword");

        Assert.That(string.IsNullOrWhiteSpace(payload.AccessToken), Is.False, "Token must be present");
    }

    [Test]
    public async Task Login_IsRateLimited()
    {
        Assert.That(_client, Is.Not.Null);

        const string ipAddress = "9.9.9.9";
        using HttpResponseMessage firstLogin = await PostLoginAsync(
            "limiteduser",
            "testpassword",
            ipAddress);
        Assert.That(firstLogin.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        for (int i = 0; i < 9; i++)
        {
            using HttpResponseMessage failedLogin = await PostLoginAsync(
                "limiteduser",
                "wrong-password",
                ipAddress);
            Assert.That(failedLogin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        using HttpResponseMessage limitedLogin = await PostLoginAsync(
            "limiteduser",
            "wrong-password",
            ipAddress);
        CottonResult? result = await limitedLogin.Content.ReadFromJsonAsync<CottonResult>();
        Assert.Multiple(() =>
        {
            Assert.That(limitedLogin.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(limitedLogin.Headers.RetryAfter?.Delta?.TotalSeconds, Is.GreaterThan(1));
            Assert.That(result?.MessageCode, Is.EqualTo("rate_limit_exceeded"));
            Assert.That(result?.Message, Is.EqualTo("Too many requests. Retry later."));
        });
    }

    [Test]
    public async Task Login_FromUntrustedProxy_ReturnsForbidden()
    {
        Assert.That(_client, Is.Not.Null);
        Assert.That(_customFactory, Is.Not.Null);

        using IServiceScope scope = _customFactory!.Services.CreateScope();
        SettingsProvider settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider>();
        ServerSettingsSnapshot settings = settingsProvider.GetServerSettings();
        IPAddress? previousTrustedProxyIpAddress = settings.TrustedProxyIpAddress;
        byte? previousTrustedProxyPrefixLength = settings.TrustedProxyPrefixLength;
        await settingsProvider.UpdateSettingsAsync(
            current =>
            {
                current.TrustedProxyIpAddress = IPAddress.Parse("192.0.2.10");
                current.TrustedProxyPrefixLength = null;
            },
            fallbackPublicBaseUrl: null);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new CottonLoginRequestDto
                {
                    Username = "untrusted-proxy",
                    Password = "testpassword"
                })
            };
            request.Headers.Add(TestAppFactory.RemoteIpAddressHeader, "192.0.2.11");
            request.Headers.Add("CF-Connecting-IP", "203.0.113.40");

            using HttpResponseMessage response = await _client!.SendAsync(request);
            CottonResult? result = await response.Content.ReadFromJsonAsync<CottonResult>();

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                Assert.That(result?.MessageCode, Is.EqualTo("untrusted_proxy_connection"));
                Assert.That(result?.Message, Does.Not.Contain("192.0.2.10"));
                Assert.That(result?.Message, Does.Not.Contain("192.0.2.11"));
            });
        }
        finally
        {
            await settingsProvider.UpdateSettingsAsync(
                current =>
                {
                    current.TrustedProxyIpAddress = previousTrustedProxyIpAddress;
                    current.TrustedProxyPrefixLength = previousTrustedProxyPrefixLength;
                },
                fallbackPublicBaseUrl: null);
        }
    }

    [Test]
    public async Task Login_StoresClientDeviceNameInSession()
    {
        Assert.That(_client, Is.Not.Null);

        using HttpResponseMessage login = await PostLoginAsync(
            "deviceuser",
            "testpassword",
            "8.8.4.4",
            "Cotton Sync Desktop (CI workstation)");
        login.EnsureSuccessStatusCode();

        TokenPairResponseDto? payload = await login.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(payload, Is.Not.Null);

        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.AccessToken);
        List<SessionDto>? sessions = await _client.GetFromJsonAsync<List<SessionDto>>("/api/v1/auth/sessions");

        Assert.That(
            sessions?.Single(session => session.IsCurrentSession).Device,
            Is.EqualTo("Cotton Sync Desktop (CI workstation)"));
    }

    [Test]
    public async Task RevokeSession_Invalidates_Current_AccessToken()
    {
        Assert.That(_client, Is.Not.Null);

        TokenPairResponseDto login = await LoginAsync("testuser", "testpassword");
        string sessionId = new JwtSecurityTokenHandler()
            .ReadJwtToken(login.AccessToken)
            .Claims
            .First(c => c.Type == JwtRegisteredClaimNames.Sid)
            .Value;

        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        using HttpResponseMessage beforeRevoke = await _client.GetAsync("/api/v1/auth/me");
        Assert.That(beforeRevoke.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using HttpResponseMessage revoke = await _client.DeleteAsync($"/api/v1/auth/sessions/{sessionId}");
        Assert.That(revoke.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using HttpResponseMessage afterRevoke = await _client.GetAsync("/api/v1/auth/me");
        Assert.That(afterRevoke.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ChangePassword_Invalidates_Current_AccessToken()
    {
        Assert.That(_client, Is.Not.Null);

        TokenPairResponseDto login = await LoginAsync("testuser", "testpassword");
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using HttpResponseMessage beforeChange = await _client.GetAsync("/api/v1/auth/me");
        Assert.That(beforeChange.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using HttpResponseMessage change = await _client.PutAsJsonAsync(
            "/api/v1/users/me/password",
            new ServerChangePasswordRequestDto
            {
                OldPassword = "testpassword",
                NewPassword = "changed-testpassword"
            });
        Assert.That(change.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using HttpResponseMessage afterChange = await _client.GetAsync("/api/v1/auth/me");
        Assert.That(afterChange.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task<TokenPairResponseDto> LoginAsync(string username, string password)
    {
        using HttpResponseMessage response = await PostLoginAsync(username, password, "8.8.8.8");
        response.EnsureSuccessStatusCode();

        TokenPairResponseDto? payload = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(payload, Is.Not.Null);
        return payload!;
    }

    private Task<HttpResponseMessage> PostLoginAsync(
        string username,
        string password,
        string ipAddress,
        string? deviceName = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new CottonLoginRequestDto
            {
                Username = username,
                Password = password
            })
        };
        request.Headers.Add("X-Forwarded-For", ipAddress);
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            request.Headers.Add(CottonClientHeaders.DeviceName, deviceName);
        }

        return _client!.SendAsync(request);
    }
}
