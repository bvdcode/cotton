// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests
{
    [CancelAfter(10000)]
    public class CottonTokenRefreshConcurrencyTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task CanceledOwnerCompletesRotationForAnotherClient(bool directWaiter)
        {
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using BlockingRefreshHttpMessageHandler handler = new();
            using HttpClient http = new(handler);
            await using CottonCloudClient first = CreateClient(http, store);
            await using CottonCloudClient second = CreateClient(http, store);
            using CancellationTokenSource owner = new();
            Task<TokenPairDto> refresh = first.Auth.RefreshAsync(cancellationToken: owner.Token);
            await handler.RefreshStarted.Task.WaitAsync(TestContext.CurrentContext.CancellationToken);
            Task waiting = SendAsync(second, directWaiter, TestContext.CurrentContext.CancellationToken);

            await owner.CancelAsync();
            handler.ReleaseRefresh.SetResult();
            await Assert.CatchAsync<OperationCanceledException>(async () => await refresh);
            await waiting;

            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("new-refresh"));
            Assert.That(handler.RefreshCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CanceledWaiterDoesNotCancelTheOwner(bool directWaiter)
        {
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using BlockingRefreshHttpMessageHandler handler = new();
            using HttpClient http = new(handler);
            await using CottonCloudClient first = CreateClient(http, store);
            await using CottonCloudClient second = CreateClient(http, store);
            Task<TokenPairDto> refresh = first.Auth.RefreshAsync(cancellationToken: TestContext.CurrentContext.CancellationToken);
            await handler.RefreshStarted.Task.WaitAsync(TestContext.CurrentContext.CancellationToken);
            using CancellationTokenSource waiter = new();
            Task waiting = SendAsync(second, directWaiter, waiter.Token);

            await waiter.CancelAsync();
            await Assert.CatchAsync<OperationCanceledException>(async () => await waiting);
            Assert.That(refresh.IsCompleted, Is.False);
            handler.ReleaseRefresh.SetResult();
            await refresh;

            Assert.That(handler.RefreshCount, Is.EqualTo(1));
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("new-refresh"));
        }

        [Test]
        public async Task RotationRemainsExclusiveUntilReplacementIsSaved()
        {
            InMemoryCottonTokenStore inner = await CreateStoreAsync();
            BlockingTokenStore store = new(inner) { PauseSave = true };
            using BlockingRefreshHttpMessageHandler handler = new();
            handler.ReleaseRefresh.SetResult();
            using HttpClient http = new(handler);
            await using CottonCloudClient first = CreateClient(http, store);
            await using CottonCloudClient second = CreateClient(http, store);
            using CancellationTokenSource owner = new();
            Task<TokenPairDto> refresh = first.Auth.RefreshAsync(cancellationToken: owner.Token);
            await store.SaveStarted.Task.WaitAsync(TestContext.CurrentContext.CancellationToken);
            Task<UserDto> waiting = second.Auth.MeAsync(TestContext.CurrentContext.CancellationToken);

            await owner.CancelAsync();
            Assert.That(waiting.IsCompleted, Is.False);
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("old-refresh"));
            store.ReleaseSave.SetResult();
            await Assert.CatchAsync<OperationCanceledException>(async () => await refresh);
            await waiting;

            Assert.That(handler.RefreshCount, Is.EqualTo(1));
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("new-refresh"));
        }

        [Test]
        public async Task SignInDuringRefreshStoresNewSessionAfterRotation()
        {
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using BlockingRefreshHttpMessageHandler handler = new();
            using HttpClient http = new(handler);
            await using CottonCloudClient first = CreateClient(http, store);
            await using CottonCloudClient second = CreateClient(http, store);
            CancellationToken cancellation = TestContext.CurrentContext.CancellationToken;
            Task<TokenPairDto> refresh = first.Auth.RefreshAsync(cancellationToken: cancellation);
            await handler.RefreshStarted.Task.WaitAsync(cancellation);
            Task<TokenPairDto> login = second.Auth.LoginAsync(new LoginRequestDto { Username = "account", Password = "test" }, cancellation);
            handler.ReleaseRefresh.SetResult();
            await Task.WhenAll(refresh, login);

            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("login-refresh"));
            Assert.That(handler.RefreshCount, Is.EqualTo(1));
        }

        [Test]
        public async Task FailedPersistenceIsReportedWithoutRetryAndReleasesCoordinator()
        {
            InMemoryCottonTokenStore inner = await CreateStoreAsync();
            BlockingTokenStore store = new(inner) { FailSave = true };
            using BlockingRefreshHttpMessageHandler handler = new();
            handler.ReleaseRefresh.SetResult();
            using HttpClient http = new(handler);
            await using CottonCloudClient client = CreateClient(http, store);

            await Assert.ThrowsAsync<IOException>(async () => await client.Auth.RefreshAsync());
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("old-refresh"));
            Assert.That(handler.RefreshCount, Is.EqualTo(1));
            store.FailSave = false;
            await client.Auth.LoginAsync(new LoginRequestDto { Username = "account", Password = "test" }, TestContext.CurrentContext.CancellationToken);
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("login-refresh"));
        }

        private static async Task SendAsync(CottonCloudClient client, bool directRefresh, CancellationToken cancellation)
        {
            if (directRefresh)
            {
                await client.Auth.RefreshAsync(cancellationToken: cancellation);
            }
            else
            {
                await client.Auth.MeAsync(cancellation);
            }
        }

        private static async Task<InMemoryCottonTokenStore> CreateStoreAsync()
        {
            InMemoryCottonTokenStore store = new();
            await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "old-refresh" });
            return store;
        }

        private static CottonCloudClient CreateClient(HttpClient http, ICottonTokenStore store)
        {
            return new CottonCloudClient(http, store, new CottonSdkOptions { BaseAddress = new Uri("https://cotton.test") });
        }
    }
}
