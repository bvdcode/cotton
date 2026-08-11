// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Notifications;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests;

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

    [TestCase(0, 50)]
    [TestCase(1, 0)]
    public async Task GetNotificationsAsync_RejectsNonPositivePaging(int page, int pageSize)
    {
        CottonCloudClient client = await CreateAuthorizedClientAsync(new QueuedHttpMessageHandler());

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.Notifications.GetNotificationsAsync(page, pageSize));
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
