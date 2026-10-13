// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public class FileTransferMetadataTests : MoveEndpointTestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task Transfer_PatchesMetadataAtomically_AndPreservesSourceOnCopy(bool copy)
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto target = await CreateFolderAsync(root.Id, "target");
            NodeFileManifestDto source = await CreateFileAsync(root.Id, "opaque-source", "content");
            using HttpResponseMessage update = await _client!.PatchAsJsonAsync($"/api/v1/files/{source.Id}/metadata",
                new Dictionary<string, string> { ["en"] = "original envelope", ["isClientEncrypted"] = "true" });
            update.EnsureSuccessStatusCode();
            Dictionary<string, string?> metadata = new() { ["en"] = "renamed envelope" };
            using HttpResponseMessage response = copy
                ? await _client.PostAsJsonAsync($"/api/v1/files/{source.Id}/copy", new { parentId = target.Id, name = "opaque-copy", metadata })
                : await MoveFileAsync(source.Id, new MoveFileRequestDto { ParentId = target.Id, Metadata = metadata });
            response.EnsureSuccessStatusCode();
            NodeFileManifestDto file = (await response.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;

            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile stored = await db.NodeFiles.AsNoTracking().SingleAsync(x => x.Id == file.Id);
            NodeFile original = await db.NodeFiles.AsNoTracking().SingleAsync(x => x.Id == source.Id);
            Assert.Multiple(() =>
            {
                Assert.That(file.Metadata["en"], Is.EqualTo("renamed envelope"));
                Assert.That(stored.Metadata!["en"], Is.EqualTo("renamed envelope"));
                Assert.That(stored.Metadata["isClientEncrypted"], Is.EqualTo("true"));
                Assert.That(stored.NodeId, Is.EqualTo(target.Id));
                Assert.That(original.NodeId, Is.EqualTo(copy ? root.Id : target.Id));
                Assert.That(original.Metadata!["en"], Is.EqualTo(copy ? "original envelope" : "renamed envelope"));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task InvalidMetadata_RollsBackOverwriteAndTransfer(bool copy)
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto target = await CreateFolderAsync(root.Id, "target");
            NodeFileManifestDto source = await CreateFileAsync(root.Id, "source.txt", "source");
            NodeFileManifestDto existing = await CreateFileAsync(target.Id, "target.txt", "existing");
            Dictionary<string, string?> metadata = new() { [" "] = "invalid" };
            using HttpResponseMessage response = copy
                ? await _client!.PostAsJsonAsync($"/api/v1/files/{source.Id}/copy",
                    new { parentId = target.Id, name = existing.Name, overwrite = true, metadata })
                : await MoveFileAsync(source.Id, new MoveFileRequestDto
                { ParentId = target.Id, Name = existing.Name, Overwrite = true, Metadata = metadata });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That((await GetChildrenAsync(root.Id)).Files.Single().Id, Is.EqualTo(source.Id));
            Assert.That((await GetChildrenAsync(target.Id)).Files.Single().Id, Is.EqualTo(existing.Id));
        }
    }
}
