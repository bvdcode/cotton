// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public class EmptyFolderDeletionTests : MoveEndpointTestBase
    {
        [Test]
        public async Task DeleteOnlyIfEmpty_EmptyFolder_MovesToTrash()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "empty");

            HttpResponseMessage response = await _client!.DeleteAsync(
                $"/api/v1/layouts/nodes/{folder.Id}?onlyIfEmpty=true");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            NodeContentDto content = await GetChildrenAsync(root.Id);
            Assert.That(content.Nodes.Any(x => x.Id == folder.Id), Is.False);
        }

        [Test]
        public async Task DeleteOnlyIfEmpty_FolderWithFile_PreservesContents()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "with-file");
            NodeFileManifestDto file = await CreateFileAsync(folder.Id, "keep.txt", "keep");

            HttpResponseMessage response = await _client!.DeleteAsync(
                $"/api/v1/layouts/nodes/{folder.Id}?onlyIfEmpty=true");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            NodeContentDto content = await GetChildrenAsync(folder.Id);
            Assert.That(content.Files.Select(x => x.Id), Does.Contain(file.Id));
            Assert.That(content.Node.ParentId, Is.EqualTo(root.Id));
        }

        [Test]
        public async Task DeleteOnlyIfEmpty_FolderWithSubfolder_PreservesContents()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "with-folder");
            NodeDto child = await CreateFolderAsync(folder.Id, "keep");

            HttpResponseMessage response = await _client!.DeleteAsync(
                $"/api/v1/layouts/nodes/{folder.Id}?onlyIfEmpty=true");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            NodeContentDto content = await GetChildrenAsync(folder.Id);
            Assert.That(content.Nodes.Select(x => x.Id), Does.Contain(child.Id));
            Assert.That(content.Node.ParentId, Is.EqualTo(root.Id));
        }

        [Test]
        public async Task DeleteOnlyIfEmpty_SkipTrash_IsRejected()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "empty");

            HttpResponseMessage response = await _client!.DeleteAsync(
                $"/api/v1/layouts/nodes/{folder.Id}?onlyIfEmpty=true&skipTrash=true");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            NodeContentDto content = await GetChildrenAsync(root.Id);
            Assert.That(content.Nodes.Select(x => x.Id), Does.Contain(folder.Id));
        }
    }
}
