// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Contracts.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests;

public sealed class CottonHttpTransportTests
{
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
