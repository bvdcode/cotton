// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.IntegrationTests
{
    public class SharedLinkAccessNotifierTests
    {
        private ServiceProvider _services = null!;

        [SetUp]
        public void SetUp()
        {
            ServerSettingsCache cache = new();
            cache.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(new CottonServerSettings
            {
                TrustedProxyIpAddress = IPAddress.Any,
            }));
            ServiceCollection services = new();
            services.AddSingleton(cache);
            services.AddDbContext<CottonDbContext>(options => options.UseNpgsql(
                "Host=localhost;Database=unused;Username=unused"));
            services.AddScoped<SettingsProvider>();
            _services = services.BuildServiceProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _services.Dispose();
        }

        [Test]
        public async Task ConcurrentRequestsAcrossScopes_SendOnlyOneNotification()
        {
            using MemoryCache cache = new(new MemoryCacheOptions());
            RecordingNotificationsProvider notifications = new();
            Guid ownerId = Guid.NewGuid();
            Guid tokenId = Guid.NewGuid();
            Task[] requests = Enumerable.Range(0, 30).Select(index =>
            {
                SharedLinkAccessNotifier notifier = new(cache, notifications, new YieldingGeoLookupService());
                return notifier.NotifyOnceAsync(ownerId, tokenId, "Documents", CreateContext(), CancellationToken.None);
            }).ToArray();

            await Task.WhenAll(requests);

            Assert.That(notifications.Notifications, Has.Count.EqualTo(1));
            Assert.That(notifications.Notifications[0].Metadata!["name"], Is.EqualTo("Documents"));
            Assert.That(notifications.Notifications[0].Metadata!["i18n.titleKey"],
                Is.EqualTo(NotificationTemplateKeys.SharedLinkOpenedTitle));
        }

        [Test]
        public async Task DifferentLinksAndVisitors_AreNotSuppressed()
        {
            using MemoryCache cache = new(new MemoryCacheOptions());
            RecordingNotificationsProvider notifications = new();
            SharedLinkAccessNotifier notifier = new(cache, notifications, new YieldingGeoLookupService());
            Guid ownerId = Guid.NewGuid();
            Guid tokenId = Guid.NewGuid();

            await notifier.NotifyOnceAsync(ownerId, tokenId, "Documents", CreateContext(), CancellationToken.None);
            await notifier.NotifyOnceAsync(ownerId, tokenId, "Documents", CreateContext(), CancellationToken.None);
            await notifier.NotifyOnceAsync(ownerId, Guid.NewGuid(), "Photo", CreateContext(), CancellationToken.None);
            await notifier.NotifyOnceAsync(ownerId, tokenId, "Documents", CreateContext("1.1.1.1"), CancellationToken.None);
            await notifier.NotifyOnceAsync(ownerId, tokenId, "Documents", CreateContext(userAgent: "Firefox"), CancellationToken.None);

            Assert.That(notifications.Notifications, Has.Count.EqualTo(4));
        }

        [TestCase("HEAD")]
        [TestCase("OPTIONS")]
        public async Task TechnicalRequests_DoNotNotify(string method)
        {
            using MemoryCache cache = new(new MemoryCacheOptions());
            RecordingNotificationsProvider notifications = new();
            SharedLinkAccessNotifier notifier = new(cache, notifications, new YieldingGeoLookupService());
            DefaultHttpContext context = CreateContext();
            context.Request.Method = method;

            await notifier.NotifyOnceAsync(Guid.NewGuid(), Guid.NewGuid(), "Documents", context, CancellationToken.None);

            Assert.That(notifications.Notifications, Is.Empty);
        }

        [Test]
        public async Task FailedNotification_DoesNotSuppressTheNextAttempt()
        {
            using MemoryCache cache = new(new MemoryCacheOptions());
            RecordingNotificationsProvider notifications = new() { ThrowOnNotification = true };
            SharedLinkAccessNotifier notifier = new(cache, notifications, new YieldingGeoLookupService());
            Guid ownerId = Guid.NewGuid();
            Guid tokenId = Guid.NewGuid();

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await notifier.NotifyOnceAsync(ownerId, tokenId, "Documents", CreateContext(), CancellationToken.None));
            notifications.ThrowOnNotification = false;
            await notifier.NotifyOnceAsync(ownerId, tokenId, "Documents", CreateContext(), CancellationToken.None);

            Assert.That(notifications.Notifications, Has.Count.EqualTo(1));
        }

        private DefaultHttpContext CreateContext(string ip = "8.8.8.8", string userAgent = "Windows")
        {
            DefaultHttpContext context = new();
            context.RequestServices = _services;
            context.Request.Method = HttpMethods.Get;
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            context.Request.Headers.UserAgent = userAgent;
            return context;
        }
    }
}
