// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Services;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public class FolderMetadataEndpointsTests : LayoutFileTestBase
    {
        private async Task<NodeDto> AuthenticateAsync()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            return (await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
        }

        [Test]
        public async Task CreateAndRename_StoreMetadataTogetherWithName()
        {
            NodeDto root = await AuthenticateAsync();
            using HttpResponseMessage response = await _client!.PutAsJsonAsync("/api/v1/layouts/nodes",
                new CreateNodeRequestDto
                {
                    ParentId = root.Id,
                    Name = "opaque",
                    Metadata = new() { ["label"] = "ciphertext", ["custom"] = "kept" },
                });
            response.EnsureSuccessStatusCode();
            NodeDto created = (await response.Content.ReadFromJsonAsync<NodeDto>())!;
            Assert.That(created.Metadata["label"], Is.EqualTo("ciphertext"));
            using HttpResponseMessage renamedResponse = await _client!.PatchAsJsonAsync($"/api/v1/layouts/nodes/{created.Id}/rename",
                new RenameNodeRequestDto { Name = "Documents", Metadata = new() { ["label"] = null } });
            renamedResponse.EnsureSuccessStatusCode();
            NodeDto renamed = (await renamedResponse.Content.ReadFromJsonAsync<NodeDto>())!;
            Assert.Multiple(() =>
            {
                Assert.That(renamed.Name, Is.EqualTo("Documents"));
                Assert.That(renamed.Metadata.ContainsKey("en"), Is.False);
                Assert.That(renamed.Metadata["custom"], Is.EqualTo("kept"));
            });
        }

        [Test]
        public async Task Restore_RecreatesMissingParentsWithOpaqueMetadata()
        {
            NodeDto root = await AuthenticateAsync();
            NodeDto parent = await CreateNodeAsync(root.Id, "opaque-parent");
            Dictionary<string, string> metadata = new() { ["label"] = "opaque-display-name", ["custom"] = "retained" };
            using HttpResponseMessage metadataResponse = await _client!.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{parent.Id}/metadata", metadata);
            metadataResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto file = await UploadTextFileAsync(parent.Id, "opaque-file", "payload");
            using HttpResponseMessage trashResponse = await _client!.DeleteAsync($"/api/v1/files/{file.Id}");
            trashResponse.EnsureSuccessStatusCode();
            using HttpResponseMessage deleteResponse = await _client!.DeleteAsync(
                $"/api/v1/layouts/nodes/{parent.Id}?skipTrash=true");
            deleteResponse.EnsureSuccessStatusCode();
            using HttpResponseMessage response = await _client!.PostAsJsonAsync($"/api/v1/files/{file.Id}/restore", new { createMissingParents = true });
            response.EnsureSuccessStatusCode();
            RestoreOutcomeDto outcome = (await response.Content.ReadFromJsonAsync<RestoreOutcomeDto>())!;
            Assert.That(outcome.Status, Is.EqualTo(RestoreStatus.Restored));
            NodeDto restoredParent = (await _client!.GetFromJsonAsync<NodeDto>($"/api/v1/layouts/nodes/{outcome.RestoredFile!.NodeId}"))!;
            Assert.That(restoredParent.Metadata, Is.EquivalentTo(metadata));
            Assert.That(outcome.RestoredFile.Metadata.ContainsKey(TrashParentMetadata.Key), Is.False);
        }

        [Test]
        public async Task RenameConflict_DoesNotApplyMetadataChanges()
        {
            NodeDto root = await AuthenticateAsync();
            NodeDto source = await CreateNodeAsync(root.Id, "Source");
            await CreateNodeAsync(root.Id, "Occupied");
            using HttpResponseMessage response = await _client!.PatchAsJsonAsync($"/api/v1/layouts/nodes/{source.Id}/rename",
                new RenameNodeRequestDto { Name = "Occupied", Metadata = new() { ["label"] = "new-value" } });
            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Conflict));
            NodeDto unchanged = (await _client!.GetFromJsonAsync<NodeDto>($"/api/v1/layouts/nodes/{source.Id}"))!;
            Assert.Multiple(() =>
            {
                Assert.That(unchanged.Name, Is.EqualTo("Source"));
                Assert.That(unchanged.Metadata.ContainsKey("en"), Is.False);
            });
        }

        [Test]
        public async Task CopyAndMove_ApplyMetadataOnlyToDestination()
        {
            NodeDto root = await AuthenticateAsync();
            NodeDto source = await CreateNodeAsync(root.Id, "Source");
            NodeDto target = await CreateNodeAsync(root.Id, "Target");
            using HttpResponseMessage response = await _client!.PostAsJsonAsync($"/api/v1/layouts/nodes/{source.Id}/copy",
                new { parentId = target.Id, name = "opaque-copy", metadata = new { label = "copy-name" } });
            response.EnsureSuccessStatusCode();
            NodeDto copy = (await response.Content.ReadFromJsonAsync<NodeDto>())!;
            using HttpResponseMessage movedResponse = await _client!.PatchAsJsonAsync($"/api/v1/layouts/nodes/{copy.Id}/move",
                new MoveNodeRequestDto { ParentId = root.Id, Name = "opaque-moved", Metadata = new() { ["label"] = "moved-name" } });
            movedResponse.EnsureSuccessStatusCode();
            NodeDto moved = (await movedResponse.Content.ReadFromJsonAsync<NodeDto>())!;
            NodeDto unchanged = (await _client!.GetFromJsonAsync<NodeDto>($"/api/v1/layouts/nodes/{source.Id}"))!;
            Assert.Multiple(() =>
            {
                Assert.That(copy.Metadata["label"], Is.EqualTo("copy-name"));
                Assert.That(moved.Name, Is.EqualTo("opaque-moved"));
                Assert.That(moved.Metadata["label"], Is.EqualTo("moved-name"));
                Assert.That(moved.ParentId, Is.EqualTo(root.Id));
                Assert.That(unchanged.Metadata.ContainsKey("en"), Is.False);
                Assert.That(unchanged.Name, Is.EqualTo("Source"));
            });
        }

        [Test]
        public async Task Restore_UsesSurvivingParentIdAfterRename()
        {
            NodeDto root = await AuthenticateAsync();
            NodeDto parent = await CreateNodeAsync(root.Id, "Documents");
            NodeFileManifestDto file = await UploadTextFileAsync(parent.Id, "report", "payload");
            using HttpResponseMessage trashResponse = await _client!.DeleteAsync($"/api/v1/files/{file.Id}");
            trashResponse.EnsureSuccessStatusCode();
            using HttpResponseMessage renameResponse = await _client!.PatchAsJsonAsync(
                $"/api/v1/layouts/nodes/{parent.Id}/rename", new RenameNodeRequestDto { Name = "Renamed" });
            renameResponse.EnsureSuccessStatusCode();
            using HttpResponseMessage response = await _client!.PostAsJsonAsync($"/api/v1/files/{file.Id}/restore", new { createMissingParents = false });
            response.EnsureSuccessStatusCode();
            RestoreOutcomeDto outcome = (await response.Content.ReadFromJsonAsync<RestoreOutcomeDto>())!;
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(RestoreStatus.Restored));
                Assert.That(outcome.RestoredFile?.NodeId, Is.EqualTo(parent.Id));
            });
        }
    }
}
