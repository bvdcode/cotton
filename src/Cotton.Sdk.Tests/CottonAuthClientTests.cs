// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton;
using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests;

public class CottonAuthClientTests
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
            Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/auth/logout?refreshToken=refresh%20token"));
        });
    }

    [Test]
    public async Task LoginAsync_SendsConfiguredUserAgentAndDeviceName()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "access", refreshToken = "refresh" });
        var store = new InMemoryCottonTokenStore();
        var client = new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
        {
            BaseAddress = new Uri("https://cotton.test"),
            UserAgent = "CottonSyncDesktop/1.2.3 (Linux; X64)",
            DeviceName = "Cotton Sync Desktop (workstation)",
        });

        await client.Auth.LoginAsync(new LoginRequestDto
        {
            Username = "demo",
            Password = "secret",
        });

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests[0].Headers["User-Agent"], Is.EqualTo("CottonSyncDesktop/1.2.3,(Linux; X64)"));
            Assert.That(
                handler.Requests[0].Headers[CottonClientHeaders.DeviceName],
                Is.EqualTo("Cotton Sync Desktop (workstation)"));
        });
    }

    [Test]
    public async Task LoginAsync_TruncatesConfiguredDeviceName()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "access", refreshToken = "refresh" });
        var store = new InMemoryCottonTokenStore();
        string longDeviceName = new('d', CottonClientHeaders.DeviceNameMaxLength + 1);
        var client = new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
        {
            BaseAddress = new Uri("https://cotton.test"),
            DeviceName = longDeviceName,
        });

        await client.Auth.LoginAsync(new LoginRequestDto
        {
            Username = "demo",
            Password = "secret",
        });

        Assert.That(
            handler.Requests[0].Headers[CottonClientHeaders.DeviceName],
            Has.Length.EqualTo(CottonClientHeaders.DeviceNameMaxLength));
    }

    [Test]
    public async Task RefreshAsync_SendsConfiguredUserAgentAndDeviceName()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "access", refreshToken = "refresh" });
        var store = new InMemoryCottonTokenStore();
        await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old-refresh" });
        var client = new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
        {
            BaseAddress = new Uri("https://cotton.test"),
            UserAgent = "CottonSyncDesktop/1.2.3 (Windows; X64)",
            DeviceName = "Cotton Sync Desktop (laptop)",
        });

        await client.Auth.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests[0].Headers["User-Agent"], Is.EqualTo("CottonSyncDesktop/1.2.3,(Windows; X64)"));
            Assert.That(
                handler.Requests[0].Headers[CottonClientHeaders.DeviceName],
                Is.EqualTo("Cotton Sync Desktop (laptop)"));
        });
    }

    [Test]
    public async Task LogoutAsync_ClearsStoreWhenServerLogoutFails()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized);
        var store = new InMemoryCottonTokenStore();
        await store.SaveAsync(new TokenPairDto { AccessToken = "access", RefreshToken = "revoked" });
        var client = CreateClient(handler, store);

        Assert.ThrowsAsync<CottonApiException>(async () => await client.Auth.LogoutAsync());

        TokenPairDto? stored = await store.GetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Null);
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/auth/logout?refreshToken=revoked"));
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
