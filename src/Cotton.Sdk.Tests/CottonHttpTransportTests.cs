// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Contracts.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace Cotton.Sdk.Tests;

public sealed class CottonHttpTransportTests
{
    [Test]
    public async Task RequestLogging_RecordsMethodPathAndStatus()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new
        {
            version = "1.2.3",
            maxChunkSizeBytes = 4194304,
            supportedHashAlgorithm = "SHA256",
        });
        var loggerFactory = new RecordingLoggerFactory();
        var client = new CottonCloudClient(
            new HttpClient(handler),
            new InMemoryCottonTokenStore(),
            new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            },
            loggerFactory);

        await client.Settings.GetAsync();

        IReadOnlyList<string> messages = loggerFactory.Entries.Select(static entry => entry.Message).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(loggerFactory.Entries.Select(static entry => entry.Level), Does.Contain(LogLevel.Debug));
            Assert.That(messages, Has.Some.Contains("Sending Cotton API request GET /api/v1/settings"));
            Assert.That(messages, Has.Some.Contains("Cotton API request GET /api/v1/settings completed with status 200"));
        });
    }

    [Test]
    public async Task RequestLogging_RedactsRefreshTokenQueryValues()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, "expired");
        handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "new-access", refreshToken = "new-refresh" });
        handler.EnqueueJson(HttpStatusCode.OK, new
        {
            version = "1.2.3",
            maxChunkSizeBytes = 4194304,
            supportedHashAlgorithm = "SHA256",
        });
        var loggerFactory = new RecordingLoggerFactory();
        var store = new InMemoryCottonTokenStore();
        await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "secret refresh token" });
        var client = new CottonCloudClient(
            new HttpClient(handler),
            store,
            new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            },
            loggerFactory);

        await client.Settings.GetAsync();

        string logs = string.Join(Environment.NewLine, loggerFactory.Entries.Select(static entry => entry.Message));
        Assert.Multiple(() =>
        {
            Assert.That(logs, Does.Not.Contain("secret refresh token"));
            Assert.That(logs, Does.Not.Contain("secret%20refresh%20token"));
            Assert.That(logs, Does.Contain("/api/v1/auth/refresh?refreshToken=***"));
            Assert.That(logs, Does.Contain("token refresh succeeded"));
        });
    }

    [Test]
    public async Task AuthorizedRequest_RefreshesOnUnauthorizedAndRetriesWithNewAccessToken()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, "expired");
        handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "new-access", refreshToken = "new-refresh" });
        handler.EnqueueJson(HttpStatusCode.OK, new
        {
            version = "1.2.3",
            maxChunkSizeBytes = 4194304,
            supportedHashAlgorithm = "SHA256",
        });
        var store = new InMemoryCottonTokenStore();
        await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old refresh" });
        var client = new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
        {
            BaseAddress = new Uri("https://cotton.test"),
        });

        var settings = await client.Settings.GetAsync();
        TokenPairDto? stored = await store.GetAsync();

        Assert.Multiple(() =>
        {
            Assert.That(settings.MaxChunkSizeBytes, Is.EqualTo(4194304));
            Assert.That(stored?.AccessToken, Is.EqualTo("new-access"));
            Assert.That(handler.Requests.Select(x => x.PathAndQuery), Is.EqualTo(new[]
            {
                "/api/v1/settings",
                "/api/v1/auth/refresh?refreshToken=old%20refresh",
                "/api/v1/settings",
            }));
            Assert.That(handler.Requests[0].AuthorizationParameter, Is.EqualTo("old-access"));
            Assert.That(handler.Requests[1].AuthorizationParameter, Is.Null);
            Assert.That(handler.Requests[2].AuthorizationParameter, Is.EqualTo("new-access"));
        });
    }
}
