// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Notifications;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests
{
    public class CottonNotificationClientTests
    {
        [Test]
        public async Task GetNotificationsAsync_MapsPagedRequestAndResponse()
        {
            Guid notificationId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            DateTime createdAt = DateTime.UtcNow.AddMinutes(-1);
            var handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(
                HttpStatusCode.OK,
                new[]
                {
                    new
                    {
                        id = notificationId,
                        createdAt,
                        updatedAt = createdAt,
                        title = "Storage warning",
                        content = "Free space is low.",
                        readAt = (DateTime?)null,
                        metadata = new Dictionary<string, string> { ["kind"] = "storage-pressure" },
                        userId,
                        priority = 3,
                    }
                },
                new Dictionary<string, string> { ["X-Total-Count"] = "41" });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);

            CottonPagedResult<IReadOnlyList<CottonNotificationDto>> result =
                await client.Notifications.GetNotificationsAsync(page: 2, pageSize: 25);

            CottonNotificationDto notification = result.Payload.Single();
            Assert.Multiple(() =>
            {
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/notifications?page=2&pageSize=25"));
                Assert.That(result.TotalCount, Is.EqualTo(41));
                Assert.That(notification.Id, Is.EqualTo(notificationId));
                Assert.That(notification.CreatedAt, Is.EqualTo(createdAt).Within(TimeSpan.FromMilliseconds(1)));
                Assert.That(notification.Title, Is.EqualTo("Storage warning"));
                Assert.That(notification.Metadata?["kind"], Is.EqualTo("storage-pressure"));
                Assert.That(notification.UserId, Is.EqualTo(userId));
                Assert.That(notification.Priority, Is.EqualTo(CottonNotificationPriority.High));
            });
        }

        [Test]
        public async Task GetNotificationBatchAsync_MapsCursorAndBatch()
        {
            Guid notificationId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
            Guid userId = Guid.NewGuid();
            DateTime cursorCreatedAt = new(2026, 8, 12, 20, 15, 30, DateTimeKind.Utc);
            DateTime notificationCreatedAt = cursorCreatedAt.AddMinutes(1);
            var handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(
                HttpStatusCode.OK,
                new
                {
                    unreadNotifications = new[]
                    {
                        new
                        {
                            id = notificationId,
                            createdAt = notificationCreatedAt,
                            updatedAt = notificationCreatedAt,
                            title = "New sign-in",
                            content = "A new location was detected.",
                            readAt = (DateTime?)null,
                            metadata = (Dictionary<string, string>?)null,
                            userId,
                            priority = 3,
                        }
                    },
                    unreadCount = 1825,
                    nextCursor = new
                    {
                        createdAt = notificationCreatedAt,
                        notificationId,
                    },
                });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);
            CottonNotificationCursorDto cursor = new()
            {
                CreatedAt = cursorCreatedAt,
                NotificationId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            };

            CottonNotificationBatchDto result = await client.Notifications
                .GetNotificationBatchAsync(cursor, detailLimit: 25);

            CottonNotificationDto notification = result.UnreadNotifications.Single();
            Assert.Multiple(() =>
            {
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(
                    handler.Requests[0].PathAndQuery,
                    Is.EqualTo(
                        "/api/v1/notifications/batch?detailLimit=25"
                        + "&cursorCreatedAt=2026-08-12T20%3A15%3A30.0000000Z"
                        + "&cursorNotificationId=00000000-0000-0000-0000-000000000001"));
                Assert.That(result.UnreadCount, Is.EqualTo(1825));
                Assert.That(result.NextCursor?.CreatedAt, Is.EqualTo(notificationCreatedAt));
                Assert.That(result.NextCursor?.NotificationId, Is.EqualTo(notificationId));
                Assert.That(notification.Id, Is.EqualTo(notificationId));
                Assert.That(notification.Title, Is.EqualTo("New sign-in"));
                Assert.That(notification.Priority, Is.EqualTo(CottonNotificationPriority.High));
            });
        }

        [Test]
        public async Task GetNotificationBatchAsync_WithoutCursor_RequestsCurrentUnreadBacklog()
        {
            var handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(
                HttpStatusCode.OK,
                new
                {
                    unreadNotifications = Array.Empty<object>(),
                    unreadCount = 0,
                    nextCursor = (object?)null,
                });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);

            CottonNotificationBatchDto result = await client.Notifications.GetNotificationBatchAsync();

            Assert.Multiple(() =>
            {
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/notifications/batch?detailLimit=50"));
                Assert.That(result.UnreadNotifications, Is.Empty);
                Assert.That(result.UnreadCount, Is.Zero);
                Assert.That(result.NextCursor, Is.Null);
            });
        }

        [TestCase(0, 50)]
        [TestCase(1, 0)]
        public async Task GetNotificationsAsync_RejectsNonPositivePaging(int page, int pageSize)
        {
            CottonCloudClient client = await CreateAuthorizedClientAsync(new QueuedHttpMessageHandler());

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => client.Notifications.GetNotificationsAsync(page, pageSize));
        }

        [Test]
        public async Task GetNotificationBatchAsync_RejectsNonPositiveDetailLimit()
        {
            CottonCloudClient client = await CreateAuthorizedClientAsync(new QueuedHttpMessageHandler());

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => client.Notifications.GetNotificationBatchAsync(detailLimit: 0));
        }

        [Test]
        public async Task GetNotificationBatchAsync_PropagatesCancellation()
        {
            CottonCloudClient client = await CreateAuthorizedClientAsync(new QueuedHttpMessageHandler());
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                () => client.Notifications.GetNotificationBatchAsync(cancellationToken: cancellation.Token));
        }

        private static async Task<CottonCloudClient> CreateAuthorizedClientAsync(QueuedHttpMessageHandler handler)
        {
            var store = new InMemoryCottonTokenStore();
            await store.SaveAsync(new TokenPairDto { AccessToken = "access", RefreshToken = "refresh" });
            return new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            });
        }
    }
}
