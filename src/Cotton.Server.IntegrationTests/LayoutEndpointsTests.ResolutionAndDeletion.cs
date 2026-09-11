// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class LayoutEndpointsTests
    {
        [Test]
        public async Task ResolveOwnedNodes_ReturnsOwnedDefaultNodesInRequestedOrder()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto first = await CreateNodeAsync(root!.Id, "first-pinned");
            NodeDto second = await CreateNodeAsync(root.Id, "second-pinned");

            HttpResponseMessage response = await _client.PostAsJsonAsync(
                "/api/v1/layouts/nodes/resolve",
                new[] { second.Id, Guid.NewGuid(), first.Id, second.Id });
            response.EnsureSuccessStatusCode();
            NodeDto[]? nodes = await response.Content.ReadFromJsonAsync<NodeDto[]>();

            Assert.That(nodes?.Select(node => node.Id), Is.EqualTo(new[] { second.Id, first.Id }));
        }

        [Test]
        public async Task ResolveOwnedNodes_RejectsMoreThan128Ids()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Guid[] ids = Enumerable.Range(0, 129).Select(_ => Guid.NewGuid()).ToArray();

            HttpResponseMessage response = await _client.PostAsJsonAsync(
                "/api/v1/layouts/nodes/resolve",
                ids);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task DeleteNodePermanently_RemovesRestrictedShareToken()
        {
            string accessToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto sharedNode = await CreateNodeAsync(root!.Id, "shared-delete");
            const string shareToken = "delete-node-share-token";

            using HttpResponseMessage createShare = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{sharedNode.Id}/share-link?customToken={shareToken}");
            createShare.EnsureSuccessStatusCode();
            using HttpResponseMessage sharedBeforeDelete = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}");
            sharedBeforeDelete.EnsureSuccessStatusCode();

            using HttpResponseMessage delete = await _client.DeleteAsync(
                $"/api/v1/layouts/nodes/{sharedNode.Id}?skipTrash=true");
            delete.EnsureSuccessStatusCode();
            using HttpResponseMessage sharedAfterDelete = await _client.GetAsync(
                $"/api/v1/layouts/shared/{shareToken}");

            Assert.That(sharedAfterDelete.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

    }
}
