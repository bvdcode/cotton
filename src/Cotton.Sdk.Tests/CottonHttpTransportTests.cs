// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text;
using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace Cotton.Sdk.Tests
{
    public class CottonHttpTransportTests
    {
        [Test]
        public async Task Client_ReusesAlreadyStartedHttpClientWhenBaseAddressMatches()
        {
            var handler = new QueuedHttpMessageHandler();
            handler.Enqueue(HttpStatusCode.OK, "warmup");
            handler.EnqueueJson(HttpStatusCode.OK, new
            {
                version = "1.2.3",
                maxChunkSizeBytes = 4194304,
                supportedHashAlgorithm = "SHA256",
            });
            var baseAddress = new Uri("https://cotton.test");
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = baseAddress,
            };
            using HttpResponseMessage warmupResponse = await httpClient.GetAsync("/warmup");
            warmupResponse.EnsureSuccessStatusCode();
            var client = new CottonCloudClient(
                httpClient,
                new InMemoryCottonTokenStore(),
                new CottonSdkOptions
                {
                    BaseAddress = baseAddress,
                });

            var settings = await client.Settings.GetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(settings.MaxChunkSizeBytes, Is.EqualTo(4194304));
                Assert.That(handler.Requests.Select(static request => request.PathAndQuery), Is.EqualTo(new[]
                {
                    "/warmup",
                    "/api/v1/settings",
                }));
            });
        }

        [Test]
        public void Client_RejectsPreconfiguredHttpClientWhenBaseAddressDiffers()
        {
            var httpClient = new HttpClient(new QueuedHttpMessageHandler())
            {
                BaseAddress = new Uri("https://other.test"),
            };

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => new CottonCloudClient(
                httpClient,
                new InMemoryCottonTokenStore(),
                new CottonSdkOptions
                {
                    BaseAddress = new Uri("https://cotton.test"),
                }));

            Assert.That(exception?.Message, Does.Contain("BaseAddress"));
        }

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

        [Test]
        public async Task AuthorizedRequest_PreservesBaseAddressPathForApiAndRefreshRequests()
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
                BaseAddress = new Uri("https://cotton.test/cloud"),
            });

            await client.Settings.GetAsync();

            Assert.That(handler.Requests.Select(x => x.PathAndQuery), Is.EqualTo(new[]
            {
                "/cloud/api/v1/settings",
                "/cloud/api/v1/auth/refresh?refreshToken=old%20refresh",
                "/cloud/api/v1/settings",
            }));
        }

        [Test]
        public async Task AuthorizedRequest_UsesSingleRefreshForConcurrentUnauthorizedRequests()
        {
            var handler = new ConcurrentRefreshHttpMessageHandler();
            var store = new InMemoryCottonTokenStore();
            await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old-refresh" });
            var client = new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            });

            var first = client.Settings.GetAsync();
            var second = client.Settings.GetAsync();
            var results = await Task.WhenAll(first, second);
            TokenPairDto? stored = await store.GetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(results.Select(static settings => settings.MaxChunkSizeBytes), Is.EqualTo(new[]
                {
                    4194304,
                    4194304,
                }));
                Assert.That(stored?.AccessToken, Is.EqualTo("new-access"));
                Assert.That(stored?.RefreshToken, Is.EqualTo("new-refresh"));
                Assert.That(handler.OldAccessSettingsRequestCount, Is.EqualTo(2));
                Assert.That(handler.NewAccessSettingsRequestCount, Is.EqualTo(2));
                Assert.That(handler.RefreshRequestCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void SendJsonAsync_ReportsInvalidJsonResponseWithContentPreview()
        {
            var handler = new QueuedHttpMessageHandler();
            handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<!doctype html><html>Not the API</html>", Encoding.UTF8, "text/html"),
            });
            var client = new CottonCloudClient(new HttpClient(handler), new InMemoryCottonTokenStore(), new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            });

            CottonApiException? exception = Assert.ThrowsAsync<CottonApiException>(
                async () => await client.Settings.GetAsync());

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Message, Does.Contain("invalid JSON"));
                Assert.That(exception.Message, Does.Contain("GET /api/v1/settings"));
                Assert.That(exception.Message, Does.Contain("text/html"));
                Assert.That(exception.Message, Does.Contain("<!doctype html>"));
                Assert.That(exception.ResponseBody, Does.Contain("Not the API"));
            });
        }

        [Test]
        public void SendNoContentAsync_IncludesFailureResponsePreview()
        {
            var handler = new QueuedHttpMessageHandler();
            handler.Enqueue(HttpStatusCode.BadRequest, "Validation failed for remote folder.");
            var client = new CottonCloudClient(new HttpClient(handler), new InMemoryCottonTokenStore(), new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            });

            CottonApiException? exception = Assert.ThrowsAsync<CottonApiException>(
                async () => await client.Nodes.DeleteAsync(Guid.NewGuid()));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Message, Does.Contain("DELETE /api/v1/layouts/nodes/"));
                Assert.That(exception!.Message, Does.Contain("400"));
                Assert.That(exception.Message, Does.Contain("Validation failed for remote folder."));
                Assert.That(exception.ResponseBody, Is.EqualTo("Validation failed for remote folder."));
            });
        }
    }
}
