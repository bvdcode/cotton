// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class SyncChangesEndpointsTests
    {
        [Test]
        public async Task RestoreFolder_WithMissingParentCreation_StagesParentFolderCreatedBeforeFolderRestored()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto parent = await CreateFolderAsync(root.Id, "folder-restore-created-parent");
            NodeDto folder = await CreateFolderAsync(parent.Id, "sync-restored-folder-with-parent");
            using HttpResponseMessage deleteFolderResponse = await _client!.DeleteAsync($"{Routes.V1.Layouts}/nodes/{folder.Id}");
            deleteFolderResponse.EnsureSuccessStatusCode();
            using HttpResponseMessage deleteParentResponse = await _client.DeleteAsync($"{Routes.V1.Layouts}/nodes/{parent.Id}");
            deleteParentResponse.EnsureSuccessStatusCode();
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage restoreResponse = await _client.PostAsJsonAsync(
                $"{Routes.V1.Layouts}/nodes/{folder.Id}/restore",
                new RestoreItemRequestDto { CreateMissingParents = true });
            restoreResponse.EnsureSuccessStatusCode();

            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);
            SyncChangeDto parentCreated = response.Changes.Single(x =>
                x.Kind == SyncChangeKind.FolderCreated && x.Name == "folder-restore-created-parent");
            SyncChangeDto folderRestored = response.Changes.Single(x => x.ItemId == folder.Id);

            Assert.Multiple(() =>
            {
                Assert.That(parentCreated.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(folderRestored.Kind, Is.EqualTo(SyncChangeKind.FolderRestored));
                Assert.That(folderRestored.ParentNodeId, Is.EqualTo(parentCreated.ItemId));
                Assert.That(folderRestored.Name, Is.EqualTo("sync-restored-folder-with-parent"));
            });
        }

        [Test]
        public async Task UpdateFileMetadata_StagesFileContentUpdatedChange()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "metadata-update-parent");
            NodeFileManifestDto file = await CreateFileAsync(folder.Id, "sync-updated-file.txt", "metadata-body");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage updateResponse = await _client!.PatchAsJsonAsync(
                $"{Routes.V1.Files}/{file.Id}/metadata",
                new Dictionary<string, string?> { ["label"] = "synced" });
            updateResponse.EnsureSuccessStatusCode();

            SyncChangeDto change = await GetSingleChangeAsync(cursor, file.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileContentUpdated));
                Assert.That(change.ParentNodeId, Is.EqualTo(folder.Id));
                Assert.That(change.FileManifestId, Is.EqualTo(file.FileManifestId));
                Assert.That(change.Name, Is.EqualTo("sync-updated-file.txt"));
            });
        }

        [Test]
        public async Task WebDavPutFile_StagesFileCreatedChange()
        {
            string accessToken = await SignInAsync();

            NodeDto root = await GetRootAsync();
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            await UseWebDavBasicAuthAsync();
            using HttpResponseMessage putResponse = await SendWebDavPutAsync(
                "/api/v1/webdav/webdav-created-file.txt",
                "webdav-created-body");
            putResponse.EnsureSuccessStatusCode();

            UseBearerAuth(accessToken);
            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);
            SyncChangeDto change = response.Changes.Single(x => x.Name == "webdav-created-file.txt");

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileCreated));
                Assert.That(change.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(change.FileManifestId, Is.Not.Null);
            });
        }

        [Test]
        public async Task WebDavMkCol_StagesFolderCreatedChange()
        {
            string accessToken = await SignInAsync();

            NodeDto root = await GetRootAsync();
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            await UseWebDavBasicAuthAsync();
            using HttpResponseMessage mkColResponse = await SendWebDavMkColAsync("/api/v1/webdav/webdav-created-folder");
            mkColResponse.EnsureSuccessStatusCode();

            UseBearerAuth(accessToken);
            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);
            SyncChangeDto change = response.Changes.Single(x => x.Name == "webdav-created-folder");

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FolderCreated));
                Assert.That(change.ParentNodeId, Is.EqualTo(root.Id));
            });
        }

        [Test]
        public async Task WebDavMoveFile_StagesFileMovedChange()
        {
            string accessToken = await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeFileManifestDto file = await CreateFileAsync(root.Id, "webdav-move-source.txt", "webdav-move-body");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            await UseWebDavBasicAuthAsync();
            using HttpResponseMessage moveResponse = await SendWebDavMoveAsync(
                "/api/v1/webdav/webdav-move-source.txt",
                "/api/v1/webdav/webdav-move-target.txt");
            moveResponse.EnsureSuccessStatusCode();

            UseBearerAuth(accessToken);
            SyncChangeDto change = await GetSingleChangeAsync(cursor, file.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileMoved));
                Assert.That(change.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(change.PreviousParentNodeId, Is.EqualTo(root.Id));
                Assert.That(change.Name, Is.EqualTo("webdav-move-target.txt"));
            });
        }

        [Test]
        public async Task WebDavCopyFile_StagesFileCreatedChange()
        {
            string accessToken = await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeFileManifestDto file = await CreateFileAsync(root.Id, "webdav-copy-source.txt", "webdav-copy-body");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            await UseWebDavBasicAuthAsync();
            using HttpResponseMessage copyResponse = await SendWebDavCopyAsync(
                "/api/v1/webdav/webdav-copy-source.txt",
                "/api/v1/webdav/webdav-copy-target.txt");
            copyResponse.EnsureSuccessStatusCode();

            UseBearerAuth(accessToken);
            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);
            SyncChangeDto change = response.Changes.Single(x => x.Name == "webdav-copy-target.txt");

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileCreated));
                Assert.That(change.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(change.FileManifestId, Is.EqualTo(file.FileManifestId));
            });
        }

        [Test]
        public async Task RestoreFileVersion_StagesFileContentUpdatedChange()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeFileManifestDto file = await CreateFileAsync(root.Id, "versioned-file.txt", "version-one");
            await UpdateFileContentAsync(file.Id, root.Id, "versioned-file.txt", "version-two");
            List<FileVersionDto> versions = await GetFileVersionsAsync(file.Id);
            FileVersionDto historicalVersion = versions.Single(x => !x.IsCurrent);
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage restoreResponse = await _client!.PostAsync(
                $"{Routes.V1.Files}/{file.Id}/versions/{historicalVersion.Id}/restore",
                null);
            restoreResponse.EnsureSuccessStatusCode();

            SyncChangeDto change = await GetSingleChangeAsync(cursor, file.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileContentUpdated));
                Assert.That(change.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(change.FileManifestId, Is.EqualTo(historicalVersion.FileManifestId));
                Assert.That(change.Name, Is.EqualTo("versioned-file.txt"));
            });
        }

    }
}
