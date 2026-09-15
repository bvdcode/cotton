// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests
{
    public class CottonTokenRefreshRecoveryTests
    {
        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.TooManyRequests)]
        [TestCase(HttpStatusCode.BadGateway)]
        [TestCase(HttpStatusCode.ServiceUnavailable)]
        [TestCase(HttpStatusCode.GatewayTimeout)]
        public async Task TemporaryRefreshFailurePreservesTokensAndRecoversWithRecreatedClient(HttpStatusCode status)
        {
            QueuedHttpMessageHandler handler = new();
            handler.Enqueue(HttpStatusCode.Unauthorized);
            handler.Enqueue(status, "temporarily unavailable");
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);

            CottonTokenRefreshException? failure = Assert.ThrowsAsync<CottonTokenRefreshException>(
                async () => await client.Auth.MeAsync());
            TokenPairDto? retained = await store.GetAsync();
            Assert.Multiple(() =>
            {
                Assert.That(failure?.StatusCode, Is.EqualTo(status));
                Assert.That(retained?.AccessToken, Is.EqualTo("old-access"));
                Assert.That(retained?.RefreshToken, Is.EqualTo("old-refresh"));
                Assert.That(handler.Requests, Has.Count.EqualTo(2));
            });

            handler.Enqueue(HttpStatusCode.Unauthorized);
            handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "new-access", refreshToken = "new-refresh" });
            handler.EnqueueJson(HttpStatusCode.OK, new { id = Guid.NewGuid(), username = "account" });
            await using CottonCloudClient restartedClient = CreateClient(httpClient, store);
            UserDto user = await restartedClient.Auth.MeAsync();
            TokenPairDto? renewed = await store.GetAsync();
            Assert.Multiple(() =>
            {
                Assert.That(user.Username, Is.EqualTo("account"));
                Assert.That(renewed?.AccessToken, Is.EqualTo("new-access"));
                Assert.That(renewed?.RefreshToken, Is.EqualTo("new-refresh"));
                Assert.That(handler.Requests, Has.Count.EqualTo(5));
                Assert.That(handler.Requests[^1].AuthorizationParameter, Is.EqualTo("new-access"));
            });
        }

        [TestCase("404 page not found")]
        [TestCase("{broken")]
        [TestCase("null")]
        [TestCase("{\"status\":404,\"title\":\"Not Found\"}")]
        [TestCase("{\"status\":404,\"code\":\"not_found\",\"instance\":\"/api/v1/files\"}")]
        [TestCase("{\"status\":\"404\",\"code\":\"not_found\",\"instance\":\"/api/v1/auth/refresh\"}")]
        public async Task UnrecognizedRefreshResponsePreservesTokens(string body)
        {
            QueuedHttpMessageHandler handler = new();
            handler.Enqueue(HttpStatusCode.Unauthorized);
            handler.Enqueue(HttpStatusCode.NotFound, body);
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);

            Assert.ThrowsAsync<CottonTokenRefreshException>(
                async () => await client.Auth.MeAsync());

            Assert.That(await store.GetAsync(), Is.Not.Null);
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RefreshRejectionPreservesTokensAndOriginalResponse(bool directRefresh)
        {
            QueuedHttpMessageHandler handler = new();
            if (!directRefresh)
            {
                handler.Enqueue(HttpStatusCode.Unauthorized);
            }

            handler.EnqueueJson(HttpStatusCode.NotFound, new
            {
                status = 404,
                code = "not_found",
                instance = "/api/v1/auth/refresh",
                detail = "Refresh token not found or revoked.",
            });
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);

            CottonTokenRefreshException? failure = Assert.ThrowsAsync<CottonTokenRefreshException>(async () =>
            {
                if (directRefresh)
                {
                    await client.Auth.RefreshAsync();
                }
                else
                {
                    await client.Auth.MeAsync();
                }
            });

            Assert.That(failure?.InnerException, Is.TypeOf<CottonApiException>());
            CottonApiException apiFailure = (CottonApiException)failure!.InnerException!;
            Assert.That(apiFailure.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(apiFailure.ResponseBody, Does.Contain("not_found"));
            TokenPairDto? retained = await store.GetAsync();
            Assert.That(retained?.AccessToken, Is.EqualTo("old-access"));
            Assert.That(retained?.RefreshToken, Is.EqualTo("old-refresh"));
            Assert.That(handler.Requests, Has.Count.EqualTo(directRefresh ? 1 : 2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ConnectionFailureDuringRefreshPreservesTokens(bool timeout)
        {
            QueuedHttpMessageHandler handler = new();
            handler.Enqueue(HttpStatusCode.Unauthorized);
            handler.Enqueue(_ =>
            {
                if (timeout)
                {
                    throw new TaskCanceledException("Request timed out.");
                }

                throw new HttpRequestException("Connection interrupted.");
            });
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);

            Assert.ThrowsAsync<CottonTokenRefreshException>(
                async () => await client.Auth.MeAsync());

            Assert.That(await store.GetAsync(), Is.Not.Null);
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task IncompleteSuccessfulRefreshPreservesExistingPair()
        {
            QueuedHttpMessageHandler handler = new();
            handler.Enqueue(HttpStatusCode.Unauthorized);
            handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "new-access" });
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);

            Assert.ThrowsAsync<CottonTokenRefreshException>(
                async () => await client.Auth.MeAsync());
            TokenPairDto? retained = await store.GetAsync();

            Assert.That(retained?.AccessToken, Is.EqualTo("old-access"));
            Assert.That(retained?.RefreshToken, Is.EqualTo("old-refresh"));
        }

        [Test]
        public async Task CallerCancellationDuringRefreshPreservesTokens()
        {
            QueuedHttpMessageHandler handler = new();
            handler.Enqueue(HttpStatusCode.Unauthorized);
            using CancellationTokenSource cancellation = new();
            handler.Enqueue(_ =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);

            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await client.Auth.MeAsync(cancellation.Token));
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("old-refresh"));
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task MissingRefreshTokenDoesNotRetryUnauthorizedRequestOrClearStore()
        {
            QueuedHttpMessageHandler handler = new();
            handler.Enqueue(HttpStatusCode.Unauthorized);
            InMemoryCottonTokenStore store = new();
            await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "" });
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);

            Assert.ThrowsAsync<CottonTokenRefreshException>(async () => await client.Auth.MeAsync());
            Assert.That((await store.GetAsync())?.AccessToken, Is.EqualTo("old-access"));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        }

        private static async Task<InMemoryCottonTokenStore> CreateStoreAsync()
        {
            InMemoryCottonTokenStore store = new();
            await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old-refresh" });
            return store;
        }

        private static CottonCloudClient CreateClient(HttpClient httpClient, ICottonTokenStore store)
        {
            return new CottonCloudClient(httpClient, store, new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            });
        }
    }
}
