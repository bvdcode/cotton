// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Contracts.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests;

public sealed class CottonAuthClientTests
{
    [Test]
    public async Task LoginAsync_PostsCredentialsAndStoresTokenPair()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "access", refreshToken = "refresh" });
        var store = new InMemoryCottonTokenStore();
        var client = CreateClient(handler, store);

        TokenPairDto tokens = await client.Auth.LoginAsync(new LoginRequestDto
        {
            Username = "demo",
            Password = "secret",
            TrustDevice = true,
            TwoFactorCode = "123456",
        });

        TokenPairDto? stored = await store.GetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(tokens.AccessToken, Is.EqualTo("access"));
            Assert.That(stored?.RefreshToken, Is.EqualTo("refresh"));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/auth/login"));
            Assert.That(handler.Requests[0].AuthorizationParameter, Is.Null);
            Assert.That(handler.Requests[0].Body, Does.Contain("\"username\":\"demo\""));
            Assert.That(handler.Requests[0].Body, Does.Contain("\"twoFactorCode\":\"123456\""));
        });
    }

    [Test]
    public async Task LogoutAsync_PostsRefreshTokenAndClearsStore()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK);
        var store = new InMemoryCottonTokenStore();
        await store.SaveAsync(new TokenPairDto { AccessToken = "access", RefreshToken = "refresh token" });
        var client = CreateClient(handler, store);

        await client.Auth.LogoutAsync();

        TokenPairDto? stored = await store.GetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Null);
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/auth/logout"));
            Assert.That(handler.Requests[0].Body, Does.Contain("\"refreshToken\":\"refresh token\""));
        });
    }

    [Test]
    public async Task RefreshAsync_PostsRefreshTokenInBodyAndStoresTokenPair()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "new-access", refreshToken = "new-refresh" });
        var store = new InMemoryCottonTokenStore();
        await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old refresh" });
        var client = CreateClient(handler, store);

        TokenPairDto tokens = await client.Auth.RefreshAsync();

        TokenPairDto? stored = await store.GetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(tokens.AccessToken, Is.EqualTo("new-access"));
            Assert.That(stored?.RefreshToken, Is.EqualTo("new-refresh"));
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/auth/refresh"));
            Assert.That(handler.Requests[0].Body, Does.Contain("\"refreshToken\":\"old refresh\""));
        });
    }

    private static CottonCloudClient CreateClient(QueuedHttpMessageHandler handler, ICottonTokenStore store)
    {
        return new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
        {
            BaseAddress = new Uri("https://cotton.test"),
        });
    }
}
