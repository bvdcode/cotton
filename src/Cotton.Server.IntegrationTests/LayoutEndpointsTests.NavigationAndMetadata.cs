// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class LayoutEndpointsTests
    {
        [Test]
        public async Task Resolve_And_Create_Node_Then_List_Ancestors_Children_Works()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            // create child
            HttpResponseMessage createNodeRes = await _client.PutAsJsonAsync("/api/v1/layouts/nodes", new CreateNodeRequestDto { ParentId = root!.Id, Name = "child" });
            createNodeRes.EnsureSuccessStatusCode();
            NodeDto? child = await createNodeRes.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(child, Is.Not.Null);

            // resolve path
            NodeDto? resolved = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver/child");
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Id, Is.EqualTo(child!.Id));

            // list ancestors
            IEnumerable<NodeDto>? ancestors = await _client.GetFromJsonAsync<IEnumerable<NodeDto>>($"/api/v1/layouts/nodes/{child!.Id}/ancestors");
            Assert.That(ancestors, Is.Not.Null);
            Assert.That(ancestors!.Any(a => a.Id == root!.Id), Is.True);

            // list children for root
            NodeContentDto? children = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
            Assert.That(children, Is.Not.Null);
            Assert.That(children!.Nodes.Any(n => n.Id == child!.Id), Is.True);
        }

        [Test]
        public async Task Update_Node_Metadata_Merges_And_Persists_String_Values()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            HttpResponseMessage createNodeRes = await _client.PutAsJsonAsync(
                "/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = root!.Id, Name = "encrypted" });
            createNodeRes.EnsureSuccessStatusCode();
            NodeDto? child = await createNodeRes.Content.ReadFromJsonAsync<NodeDto>();
            Assert.That(child, Is.Not.Null);

            HttpResponseMessage firstPatch = await _client.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{child!.Id}/metadata",
                new Dictionary<string, string>
                {
                    ["isClientEncryptionEnabled"] = "true",
                    ["color"] = "blue"
                });
            firstPatch.EnsureSuccessStatusCode();
            NodeDto? first = await firstPatch.Content.ReadFromJsonAsync<NodeDto>();

            Assert.Multiple(() =>
            {
                Assert.That(first!.Metadata["isClientEncryptionEnabled"], Is.EqualTo("true"));
                Assert.That(first.Metadata["color"], Is.EqualTo("blue"));
            });

            HttpResponseMessage secondPatch = await _client.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{child.Id}/metadata",
                new Dictionary<string, string>
                {
                    ["isClientEncryptionEnabled"] = "false"
                });
            secondPatch.EnsureSuccessStatusCode();
            NodeDto? second = await secondPatch.Content.ReadFromJsonAsync<NodeDto>();

            Assert.Multiple(() =>
            {
                Assert.That(second!.Metadata["isClientEncryptionEnabled"], Is.EqualTo("false"));
                Assert.That(second.Metadata["color"], Is.EqualTo("blue"));
            });

            NodeDto? persisted = await _client.GetFromJsonAsync<NodeDto>($"/api/v1/layouts/nodes/{child.Id}");
            Assert.Multiple(() =>
            {
                Assert.That(persisted!.Metadata["isClientEncryptionEnabled"], Is.EqualTo("false"));
                Assert.That(persisted.Metadata["color"], Is.EqualTo("blue"));
            });
        }

    }
}
