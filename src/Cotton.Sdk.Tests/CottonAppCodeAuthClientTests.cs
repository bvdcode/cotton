// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests
{
    public class CottonAppCodeAuthClientTests
    {
        [Test]
        public async Task StartAppCodeAsync_PostsMetadataAndReturnsAbsoluteApprovalUri()
        {
            var handler = new QueuedHttpMessageHandler();
            Guid approvalId = Guid.Parse("0190a000-0000-7000-8000-000000000001");
            DateTime expiresAt = new(2026, 06, 07, 12, 30, 00, DateTimeKind.Utc);
            handler.EnqueueJson(HttpStatusCode.OK, new
            {
                approvalId,
                approvalUrl = $"/oauth/app-code/{approvalId:D}",
                pollToken = "poll-token",
                expiresAt,
                pollIntervalSeconds = 2,
            });
            var client = CreateClient(handler, new InMemoryCottonTokenStore());

            AppCodeAuthorizationSession session = await client.Auth.StartAppCodeAsync(new AppCodeStartRequestDto
            {
                ApplicationName = "Cotton Sync Desktop",
                ApplicationVersion = "1.2.3",
                DeviceName = "workstation",
            });

            Assert.Multiple(() =>
            {
                Assert.That(session.ApprovalId, Is.EqualTo(approvalId));
                Assert.That(session.ApprovalUri, Is.EqualTo(new Uri($"https://cotton.test/oauth/app-code/{approvalId:D}")));
                Assert.That(session.PollToken, Is.EqualTo("poll-token"));
                Assert.That(session.ExpiresAt, Is.EqualTo(expiresAt));
                Assert.That(session.PollInterval, Is.EqualTo(TimeSpan.FromSeconds(2)));
                Assert.That(handler.Requests, Has.Count.EqualTo(1));
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/oauth/app-code/start"));
                Assert.That(handler.Requests[0].AuthorizationParameter, Is.Null);
                Assert.That(handler.Requests[0].Body, Does.Contain("\"applicationName\":\"Cotton Sync Desktop\""));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"applicationVersion\":\"1.2.3\""));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"deviceName\":\"workstation\""));
            });
        }

        [Test]
        public async Task StartAppCodeAsync_PreservesBaseAddressPathForApprovalUri()
        {
            var handler = new QueuedHttpMessageHandler();
            Guid approvalId = Guid.Parse("0190a000-0000-7000-8000-000000000001");
            handler.EnqueueJson(HttpStatusCode.OK, new
            {
                approvalId,
                approvalUrl = $"/oauth/app-code/{approvalId:D}",
                pollToken = "poll-token",
                expiresAt = DateTime.UtcNow,
                pollIntervalSeconds = 2,
            });
            var client = new CottonCloudClient(
                new HttpClient(handler),
                new InMemoryCottonTokenStore(),
                new CottonSdkOptions
                {
                    BaseAddress = new Uri("https://cotton.test/cloud"),
                });

            AppCodeAuthorizationSession session = await client.Auth.StartAppCodeAsync(new AppCodeStartRequestDto
            {
                ApplicationName = "Cotton Sync Desktop",
            });

            Assert.Multiple(() =>
            {
                Assert.That(session.ApprovalUri, Is.EqualTo(new Uri($"https://cotton.test/cloud/oauth/app-code/{approvalId:D}")));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/cloud/api/v1/oauth/app-code/start"));
            });
        }

        [Test]
        public async Task PollAppCodeAsync_StoresTokensWhenApproved()
        {
            var handler = new QueuedHttpMessageHandler();
            var store = new InMemoryCottonTokenStore();
            handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "access", refreshToken = "refresh" });
            var client = CreateClient(handler, store);

            AppCodePollResult result = await client.Auth.PollAppCodeAsync("poll-token");

            TokenPairDto? stored = await store.GetAsync();
            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(AppCodePollStatus.Approved));
                Assert.That(result.Tokens?.AccessToken, Is.EqualTo("access"));
                Assert.That(stored?.RefreshToken, Is.EqualTo("refresh"));
                Assert.That(handler.Requests, Has.Count.EqualTo(1));
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/oauth/app-code/poll"));
                Assert.That(handler.Requests[0].AuthorizationParameter, Is.Null);
                Assert.That(handler.Requests[0].Body, Does.Contain("\"pollToken\":\"poll-token\""));
            });
        }

        [Test]
        public async Task PollAppCodeAsync_ReturnsPendingRetryDelay()
        {
            var handler = new QueuedHttpMessageHandler();
            var store = new InMemoryCottonTokenStore();
            handler.EnqueueJson(HttpStatusCode.Accepted, new { error = "pending", retryAfterSeconds = 2 });
            var client = CreateClient(handler, store);

            AppCodePollResult result = await client.Auth.PollAppCodeAsync("poll-token");

            TokenPairDto? stored = await store.GetAsync();
            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(AppCodePollStatus.Pending));
                Assert.That(result.Error, Is.EqualTo("pending"));
                Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(2)));
                Assert.That(result.Tokens, Is.Null);
                Assert.That(stored, Is.Null);
            });
        }

        [TestCase(HttpStatusCode.Forbidden, "denied", AppCodePollStatus.Denied)]
        [TestCase(HttpStatusCode.Gone, "expired", AppCodePollStatus.Expired)]
        [TestCase(HttpStatusCode.NotFound, "not_found", AppCodePollStatus.NotFound)]
        [TestCase(HttpStatusCode.TooManyRequests, "too_many_requests", AppCodePollStatus.TooManyRequests)]
        public async Task PollAppCodeAsync_MapsTerminalAndRateLimitErrors(
            HttpStatusCode statusCode,
            string error,
            AppCodePollStatus expectedStatus)
        {
            var handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(statusCode, new { error, retryAfterSeconds = 3 });
            var client = CreateClient(handler, new InMemoryCottonTokenStore());

            AppCodePollResult result = await client.Auth.PollAppCodeAsync("poll-token");

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(expectedStatus));
                Assert.That(result.Error, Is.EqualTo(error));
                Assert.That(result.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(3)));
                Assert.That(result.Tokens, Is.Null);
            });
        }

        [Test]
        public void PollAppCodeAsync_RejectsBlankPollToken()
        {
            var handler = new QueuedHttpMessageHandler();
            var client = CreateClient(handler, new InMemoryCottonTokenStore());

            Assert.ThrowsAsync<ArgumentException>(async () => await client.Auth.PollAppCodeAsync(" "));
            Assert.That(handler.Requests, Is.Empty);
        }

        private static CottonCloudClient CreateClient(QueuedHttpMessageHandler handler, ICottonTokenStore store)
        {
            return new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            });
        }
    }
}
