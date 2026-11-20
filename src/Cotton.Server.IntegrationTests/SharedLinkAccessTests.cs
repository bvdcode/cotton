// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class SharedLinkAccessTests : LayoutEndpointTestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task OpeningFolder_NotifiesOwnerOnce_AndPreservesContentPrivacy(bool hidden)
        {
            string token = await CreateShareAsync(hidden);
            using HttpResponseMessage page = await _client!.GetAsync($"/s/{token}");
            page.EnsureSuccessStatusCode();
            Assert.That(await ReadNotificationsAsync(), Is.Empty);

            SharedNodeInfoDto? info = await _client.GetFromJsonAsync<SharedNodeInfoDto>(
                $"/api/v1/layouts/shared/{token}");
            Assert.That(info, Is.Not.Null);
            Assert.That(info!.Name, Is.EqualTo(hidden ? string.Empty : "Project documents"));

            await _client.GetFromJsonAsync<SharedNodeInfoDto>($"/api/v1/layouts/shared/{token}");
            if (!hidden)
            {
                using HttpResponseMessage children = await _client.GetAsync($"/api/v1/layouts/shared/{token}/children");
                children.EnsureSuccessStatusCode();
                using HttpResponseMessage stats = await _client.GetAsync($"/api/v1/layouts/shared/{token}/stats");
                stats.EnsureSuccessStatusCode();
            }

            Notification[] notifications = await ReadNotificationsAsync();
            NodeShareToken share = await DbContext.NodeShareTokens.AsNoTracking().SingleAsync(x => x.Token == token);
            Assert.Multiple(() =>
            {
                Assert.That(notifications, Has.Length.EqualTo(1));
                Assert.That(notifications[0].UserId, Is.EqualTo(share.CreatedByUserId));
                Assert.That(notifications[0].Title, Is.EqualTo("Shared link opened"));
                Assert.That(notifications[0].Metadata!["name"], Is.EqualTo("Project documents"));
            });

            using HttpRequestMessage otherVisitor = new(HttpMethod.Get, $"/api/v1/layouts/shared/{token}");
            otherVisitor.Headers.Add(TestAppFactory.RemoteIpAddressHeader, "192.168.1.2");
            using HttpResponseMessage otherResponse = await _client.SendAsync(otherVisitor);
            otherResponse.EnsureSuccessStatusCode();
            Assert.That(await ReadNotificationsAsync(), Has.Length.EqualTo(2));
        }

        [Test]
        public async Task MissingAndExpiredLinks_DoNotNotify()
        {
            string token = await CreateShareAsync(false);
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext context = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                NodeShareToken share = await context.NodeShareTokens.SingleAsync(x => x.Token == token);
                share.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                await context.SaveChangesAsync();
            }

            using HttpResponseMessage expired = await _client!.GetAsync($"/api/v1/layouts/shared/{token}");
            using HttpResponseMessage missing = await _client.GetAsync("/api/v1/layouts/shared/missing-link");
            Assert.Multiple(() =>
            {
                Assert.That(expired.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            });
            Assert.That(await ReadNotificationsAsync(), Is.Empty);
        }

        private async Task<string> CreateShareAsync(bool hidden)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            NodeDto folder = await CreateNodeAsync(root!.Id, "Project documents");
            string permissions = hidden ? "&allowUpload=true&hideFolderContent=true" : string.Empty;
            using HttpResponseMessage link = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/share-link?customToken=documents{permissions}");
            link.EnsureSuccessStatusCode();
            _client.DefaultRequestHeaders.Authorization = null;
            return "documents";
        }

        private async Task<Notification[]> ReadNotificationsAsync()
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext context = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Notification[] notifications = await context.Notifications.AsNoTracking().ToArrayAsync();
            return notifications.Where(notification => notification.Metadata?.GetValueOrDefault("i18n.titleKey")
                == NotificationTemplateKeys.SharedLinkOpenedTitle).ToArray();
        }
    }
}
