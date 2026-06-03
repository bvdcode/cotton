// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Contracts.Auth;
using Cotton.Contracts.Sync;
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
                    cursor = 42,
                    kind = 0,
                    layoutId = (Guid?)null,
                    nodeId = Guid.NewGuid(),
                    nodeFileId,
                    parentNodeId = Guid.NewGuid(),
                    previousParentNodeId = (Guid?)null,
                    fileManifestId = Guid.NewGuid(),
                    originalNodeFileId = nodeFileId,
                    name = "hello.txt",
                    contentHash = "abc",
                    eTag = "sha256-abc",
                    sizeBytes = 5,
                    createdAt = DateTime.UtcNow,
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
            Assert.That(page.Changes.Single().Kind, Is.EqualTo(SyncChangeKindDto.FileCreated));
            Assert.That(page.Changes.Single().NodeFileId, Is.EqualTo(nodeFileId));
        });
    }

    [Test]
    public async Task GetChangesAsync_RejectsNegativeCursor()
    {
        var handler = new QueuedHttpMessageHandler();
        var client = await CreateAuthorizedClientAsync(handler);

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Sync.GetChangesAsync(-1));
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
