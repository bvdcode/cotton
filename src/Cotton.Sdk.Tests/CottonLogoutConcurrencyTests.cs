// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests
{
    [CancelAfter(10000)]
    public class CottonLogoutConcurrencyTests
    {
        [TestCase(null)]
        [TestCase("old-refresh")]
        public async Task LogoutAfterStartedRefreshRevokesReplacement(string? explicitToken)
        {
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using BlockingRefreshHttpMessageHandler handler = new();
            using HttpClient http = new(handler);
            await using CottonCloudClient first = CreateClient(http, store);
            await using CottonCloudClient second = CreateClient(http, store);
            CancellationToken cancellation = TestContext.CurrentContext.CancellationToken;
            Task<TokenPairDto> refresh = first.Auth.RefreshAsync(cancellationToken: cancellation);
            await handler.RefreshStarted.Task.WaitAsync(cancellation);
            Task logout = second.Auth.LogoutAsync(explicitToken, cancellation);
            handler.ReleaseRefresh.SetResult();
            await Task.WhenAll(refresh, logout);

            Assert.That(handler.LogoutPath, Is.EqualTo("/api/v1/auth/logout?refreshToken=new-refresh"));
            Assert.That(await store.GetAsync(), Is.Null);
        }

        [Test]
        public async Task CancelingStartedLogoutStillClearsLocalSession()
        {
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using BlockingRefreshHttpMessageHandler handler = new() { PauseLogout = true };
            using HttpClient http = new(handler);
            await using CottonCloudClient client = CreateClient(http, store);
            using CancellationTokenSource cancellation = new();
            Task logout = client.Auth.LogoutAsync(cancellationToken: cancellation.Token);
            await handler.LogoutStarted.Task.WaitAsync(TestContext.CurrentContext.CancellationToken);
            await cancellation.CancelAsync();
            await Assert.CatchAsync<OperationCanceledException>(async () => await logout);

            Assert.That(await store.GetAsync(), Is.Null);
        }

        [Test]
        public async Task CancellationBeforeLogoutDoesNotSendOrClearSession()
        {
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using BlockingRefreshHttpMessageHandler handler = new();
            using HttpClient http = new(handler);
            await using CottonCloudClient client = CreateClient(http, store);
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();
            await Assert.CatchAsync<OperationCanceledException>(async () =>
                await client.Auth.LogoutAsync(cancellationToken: cancellation.Token));

            Assert.That(handler.LogoutPath, Is.Null);
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("old-refresh"));
        }

        [Test]
        public async Task ImplicitRefreshQueuedAfterLogoutDoesNotRestoreClearedTokens()
        {
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using BlockingRefreshHttpMessageHandler handler = new() { PauseLogout = true };
            using HttpClient http = new(handler);
            await using CottonCloudClient client = CreateClient(http, store);
            CancellationToken cancellation = TestContext.CurrentContext.CancellationToken;
            Task logout = client.Auth.LogoutAsync(cancellationToken: cancellation);
            await handler.LogoutStarted.Task.WaitAsync(cancellation);
            Task<TokenPairDto> refresh = client.Auth.RefreshAsync(cancellationToken: cancellation);
            handler.ReleaseLogout.SetResult();
            handler.ReleaseRefresh.SetResult();
            await logout;
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await refresh);

            Assert.That(handler.RefreshCount, Is.Zero);
            Assert.That(await store.GetAsync(), Is.Null);
        }

        [Test]
        public async Task SignInDuringLogoutIsNotClearedByOlderLogout()
        {
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using BlockingRefreshHttpMessageHandler handler = new() { PauseLogout = true };
            using HttpClient http = new(handler);
            await using CottonCloudClient client = CreateClient(http, store);
            CancellationToken cancellation = TestContext.CurrentContext.CancellationToken;
            Task logout = client.Auth.LogoutAsync(cancellationToken: cancellation);
            await handler.LogoutStarted.Task.WaitAsync(cancellation);
            Task<TokenPairDto> login = client.Auth.LoginAsync(new LoginRequestDto { Username = "account", Password = "test" }, cancellation);
            handler.ReleaseLogout.SetResult();
            await Task.WhenAll(logout, login);

            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("login-refresh"));
        }

        [Test]
        public async Task CancelingLogoutWhileQueuedLeavesRefreshAndSessionIntact()
        {
            InMemoryCottonTokenStore store = await CreateStoreAsync();
            using BlockingRefreshHttpMessageHandler handler = new();
            using HttpClient http = new(handler);
            await using CottonCloudClient client = CreateClient(http, store);
            CancellationToken timeout = TestContext.CurrentContext.CancellationToken;
            Task<TokenPairDto> refresh = client.Auth.RefreshAsync(cancellationToken: timeout);
            await handler.RefreshStarted.Task.WaitAsync(timeout);
            using CancellationTokenSource cancellation = new();
            Task logout = client.Auth.LogoutAsync(cancellationToken: cancellation.Token);
            await cancellation.CancelAsync();
            await Assert.CatchAsync<OperationCanceledException>(async () => await logout);
            handler.ReleaseRefresh.SetResult();
            await refresh;

            Assert.That(handler.LogoutPath, Is.Null);
            Assert.That((await store.GetAsync())?.RefreshToken, Is.EqualTo("new-refresh"));
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
