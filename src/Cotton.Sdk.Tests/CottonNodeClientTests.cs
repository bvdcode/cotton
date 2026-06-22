// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Auth;
using Cotton.Files;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests;

public sealed class CottonNodeClientTests
{
    [Test]
    public async Task ResolveAsync_EncodesPathAndDeserializesNode()
    {
        Guid nodeId = Guid.NewGuid();
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new
        {
            id = nodeId,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow,
            layoutId = Guid.NewGuid(),
            parentId = (Guid?)null,
            name = "docs",
            metadata = new Dictionary<string, string>(),
        });
        var client = await CreateAuthorizedClientAsync(handler);

        var node = await client.Nodes.ResolveAsync("docs/reports");

        Assert.Multiple(() =>
        {
            Assert.That(node.Id, Is.EqualTo(nodeId));
            Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/layouts/resolver/docs/reports"));
        });
    }

    [Test]
    public async Task CreateAsync_TrimsNameAndUsesPutNodesEndpoint()
    {
        Guid parentId = Guid.NewGuid();
        Guid nodeId = Guid.NewGuid();
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new
        {
            id = nodeId,
            createdAt = DateTime.UtcNow,
            updatedAt = DateTime.UtcNow,
            layoutId = Guid.NewGuid(),
            parentId,
            name = "reports",
            metadata = new Dictionary<string, string>(),
        });
        var client = await CreateAuthorizedClientAsync(handler);

        var node = await client.Nodes.CreateAsync(parentId, " reports ");

        Assert.Multiple(() =>
        {
            Assert.That(node.Name, Is.EqualTo("reports"));
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/layouts/nodes"));
            Assert.That(handler.Requests[0].Body, Does.Contain("\"name\":\"reports\""));
        });
    }

    [Test]
    public async Task RestoreAsync_MapsNonSuccessRestoreOutcome()
    {
        Guid nodeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var handler = new QueuedHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new
        {
            status = "ParentMissing",
            originalParentPath = "/Projects/Old",
            missingPath = "/Projects",
        });
        var client = await CreateAuthorizedClientAsync(handler);

        RestoreOutcomeDto outcome = await client.Nodes.RestoreAsync(nodeId);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(RestoreStatus.ParentMissing));
            Assert.That(outcome.OriginalParentPath, Is.EqualTo("/Projects/Old"));
            Assert.That(outcome.MissingPath, Is.EqualTo("/Projects"));
            Assert.That(outcome.RestoredNode, Is.Null);
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/layouts/nodes/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/restore"));
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
