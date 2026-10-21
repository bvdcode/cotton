// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests
{
    public class CottonTokenRefreshCancellationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task CancellationAfterRefreshResponsePreservesReplacementAndCancelsCaller(bool directRefresh)
        {
            using CancellationTokenSource cancellation = new();
            InMemoryCottonTokenStore inner = new();
            await inner.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old-refresh" });
            CancelingTokenStore store = new(inner, cancellation) { CancelBeforeSave = true };
            SingleUseRefreshHttpMessageHandler handler = new();
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);

            await Assert.CatchAsync<OperationCanceledException>(async () =>
            {
                if (directRefresh)
                {
                    await client.Auth.RefreshAsync(cancellationToken: cancellation.Token);
                }
                else
                {
                    await client.Auth.MeAsync(cancellation.Token);
                }
            });

            TokenPairDto? saved = await store.GetAsync();
            UserDto nextUser = await client.Auth.MeAsync();
            TokenPairDto reused = await client.Auth.RefreshAsync("old-refresh");

            Assert.Multiple(() =>
            {
                Assert.That(saved?.AccessToken, Is.EqualTo("new-access"));
                Assert.That(saved?.RefreshToken, Is.EqualTo("new-refresh"));
                Assert.That(nextUser.Username, Is.EqualTo("account"));
                Assert.That(reused.RefreshToken, Is.EqualTo("new-refresh"));
                Assert.That(handler.RefreshCount, Is.EqualTo(1));
                Assert.That(handler.RejectedRefreshCount, Is.Zero);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CancellationWhileReceivingRefreshPreservesReplacement(bool directRefresh)
        {
            using CancellationTokenSource cancellation = new();
            InMemoryCottonTokenStore store = new();
            await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old-refresh" });
            SingleUseRefreshHttpMessageHandler handler = new() { CancelDuringResponse = cancellation };
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);

            await Assert.CatchAsync<OperationCanceledException>(async () =>
            {
                if (directRefresh)
                {
                    await client.Auth.RefreshAsync(cancellationToken: cancellation.Token);
                }
                else
                {
                    await client.Auth.MeAsync(cancellation.Token);
                }
            });

            UserDto user = await client.Auth.MeAsync();
            TokenPairDto? saved = await store.GetAsync();
            Assert.Multiple(() =>
            {
                Assert.That(saved?.RefreshToken, Is.EqualTo("new-refresh"));
                Assert.That(user.Username, Is.EqualTo("account"));
                Assert.That(handler.RefreshCount, Is.EqualTo(1));
                Assert.That(handler.RejectedRefreshCount, Is.Zero);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CancellationBeforeRequestDoesNotSendOrReplaceTokens(bool directRefresh)
        {
            using CancellationTokenSource cancellation = new();
            InMemoryCottonTokenStore store = new();
            await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old-refresh" });
            SingleUseRefreshHttpMessageHandler handler = new();
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);
            await cancellation.CancelAsync();

            await Assert.CatchAsync<OperationCanceledException>(async () =>
            {
                if (directRefresh)
                {
                    await client.Auth.RefreshAsync(cancellationToken: cancellation.Token);
                }
                else
                {
                    await client.Auth.MeAsync(cancellation.Token);
                }
            });

            Assert.That(handler.RequestCount, Is.Zero);
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("old-refresh"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RefreshResponseReadUsesHttpClientTimeoutAndPreservesTokensOnFailure(bool directRefresh)
        {
            using CancellationTokenSource cancellation = new();
            InMemoryCottonTokenStore store = new();
            await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old-refresh" });
            SingleUseRefreshHttpMessageHandler handler = new()
            {
                CancelDuringResponse = cancellation,
                StallRefreshResponse = true,
            };
            using HttpClient httpClient = new(handler);
            await using CottonCloudClient client = CreateClient(httpClient, store);
            httpClient.Timeout = TimeSpan.FromMilliseconds(100);

            await Assert.ThrowsAsync<CottonTokenRefreshException>(async () =>
            {
                Task operation = directRefresh ? client.Auth.RefreshAsync() : client.Auth.MeAsync();
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
            });

            Assert.That(cancellation.IsCancellationRequested, Is.False);
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("old-refresh"));
            Assert.That(handler.RefreshCount, Is.EqualTo(1));
        }

        [Test]
        public async Task InfiniteHttpTimeoutStillFinishesRefreshBeforeReportingCallerCancellation()
        {
            using CancellationTokenSource cancellation = new();
            InMemoryCottonTokenStore store = new();
            await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old-refresh" });
            SingleUseRefreshHttpMessageHandler handler = new() { CancelDuringResponse = cancellation };
            using HttpClient httpClient = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
            await using CottonCloudClient client = CreateClient(httpClient, store);

            await Assert.CatchAsync<OperationCanceledException>(async () =>
                await client.Auth.RefreshAsync(cancellationToken: cancellation.Token));

            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("new-refresh"));
            Assert.That(handler.RefreshCount, Is.EqualTo(1));
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
