// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text;
using Cotton.Shared.Contracts.Auth;
using Cotton.Shared.Contracts.Sync;
using Cotton.Shared.Models.Enums;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests;

public sealed class CottonSyncClientTests
{
    [Test]
    public async Task GetChangesAsync_MapsCursorRequestAndResponse()
    {
        Guid nodeFileId = Guid.NewGuid();
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new
        {
            sinceCursor = 41,
            nextCursor = 42,
            hasMore = false,
            cursorExpired = false,
            earliestAvailableCursor = 40,
            changes = new[]
            {
                new
                {
                    id = 42,
                    kind = 1,
                    layoutId = Guid.NewGuid(),
                    itemId = nodeFileId,
                    parentNodeId = Guid.NewGuid(),
                    previousParentNodeId = (Guid?)null,
                    fileManifestId = Guid.NewGuid(),
                    name = "hello.txt",
                    createdAt = DateTime.UtcNow,
                    updatedAt = DateTime.UtcNow,
                }
            }
        });
        var client = await CreateAuthorizedClientAsync(handler);

        SyncChangesResponseDto page = await client.Sync.GetChangesAsync(41, 25);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/sync/changes?since=41&limit=25"));
            Assert.That(page.NextCursor, Is.EqualTo(42));
            Assert.That(page.EarliestAvailableCursor, Is.EqualTo(40));
            Assert.That(page.CursorExpired, Is.False);
            Assert.That(page.Changes.Single().Kind, Is.EqualTo(SyncChangeKind.FileCreated));
            Assert.That(page.Changes.Single().ItemId, Is.EqualTo(nodeFileId));
        });
    }

    [Test]
    public async Task GetChangesAsync_RejectsNegativeCursor()
    {
        var handler = new QueuedHttpMessageHandler();
        var client = await CreateAuthorizedClientAsync(handler);

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Sync.GetChangesAsync(-1));
    }

    [Test]
    public async Task GetChangesAsync_ReportsHtmlSpaFallbackAsApiException()
    {
        var handler = new QueuedHttpMessageHandler();
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<!doctype html><html>App</html>", Encoding.UTF8, "text/html"),
        });
        var client = await CreateAuthorizedClientAsync(handler);

        CottonApiException? exception = Assert.ThrowsAsync<CottonApiException>(
            async () => await client.Sync.GetChangesAsync(0, 10));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("GET /api/v1/sync/changes?since=0&limit=10"));
            Assert.That(exception.Message, Does.Contain("invalid JSON"));
            Assert.That(exception.Message, Does.Contain("text/html"));
            Assert.That(exception.Message, Does.Contain("<!doctype html>"));
            Assert.That(exception.ResponseBody, Does.StartWith("<!doctype html>"));
        });
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
