// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class SyncChangesEndpointsTests
    {
        [Test]
        public async Task DeleteFolder_StagesFolderDeletedChangeWithOriginalParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "sync-deleted-folder");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage deleteResponse = await _client!.DeleteAsync($"{Routes.V1.Layouts}/nodes/{folder.Id}");
            deleteResponse.EnsureSuccessStatusCode();

            SyncChangeDto change = await GetSingleChangeAsync(cursor, folder.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FolderDeleted));
                Assert.That(change.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(change.Name, Is.EqualTo("sync-deleted-folder"));
            });
        }

        [Test]
        public async Task PermanentDeleteFolderFromTrash_DoesNotStageSecondFolderDeletedChange()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "trash-cleanup-folder");

            using HttpResponseMessage trashResponse = await _client!.DeleteAsync($"{Routes.V1.Layouts}/nodes/{folder.Id}");
            trashResponse.EnsureSuccessStatusCode();
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage permanentDeleteResponse = await _client.DeleteAsync(
                $"{Routes.V1.Layouts}/nodes/{folder.Id}?skipTrash=true");
            permanentDeleteResponse.EnsureSuccessStatusCode();

            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);

            Assert.That(response.Changes.Select(x => x.ItemId), Does.Not.Contain(folder.Id));
        }

        [Test]
        public async Task RestoreFile_StagesFileRestoredChangeWithRestoredParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "restore-file-parent");
            NodeFileManifestDto file = await CreateFileAsync(folder.Id, "sync-restored-file.txt", "restore-file-body");
            using HttpResponseMessage deleteResponse = await _client!.DeleteAsync($"{Routes.V1.Files}/{file.Id}");
            deleteResponse.EnsureSuccessStatusCode();
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage restoreResponse = await _client!.PostAsJsonAsync(
                $"{Routes.V1.Files}/{file.Id}/restore",
                new RestoreItemRequestDto());
            restoreResponse.EnsureSuccessStatusCode();

            SyncChangeDto change = await GetSingleChangeAsync(cursor, file.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileRestored));
                Assert.That(change.ParentNodeId, Is.EqualTo(folder.Id));
                Assert.That(change.FileManifestId, Is.EqualTo(file.FileManifestId));
                Assert.That(change.Name, Is.EqualTo("sync-restored-file.txt"));
            });
        }

        [Test]
        public async Task RestoreFolder_StagesFolderRestoredChangeWithRestoredParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "sync-restored-folder");
            using HttpResponseMessage deleteResponse = await _client!.DeleteAsync($"{Routes.V1.Layouts}/nodes/{folder.Id}");
            deleteResponse.EnsureSuccessStatusCode();
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage restoreResponse = await _client!.PostAsJsonAsync(
                $"{Routes.V1.Layouts}/nodes/{folder.Id}/restore",
                new RestoreItemRequestDto());
            restoreResponse.EnsureSuccessStatusCode();

            SyncChangeDto change = await GetSingleChangeAsync(cursor, folder.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FolderRestored));
                Assert.That(change.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(change.Name, Is.EqualTo("sync-restored-folder"));
            });
        }

        [Test]
        public async Task RestoreFile_WithMissingParentCreation_StagesParentFolderCreatedBeforeFileRestored()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto parent = await CreateFolderAsync(root.Id, "file-restore-created-parent");
            NodeFileManifestDto file = await CreateFileAsync(parent.Id, "sync-restored-with-parent.txt", "restore-created-parent-body");
            using HttpResponseMessage deleteFileResponse = await _client!.DeleteAsync($"{Routes.V1.Files}/{file.Id}");
            deleteFileResponse.EnsureSuccessStatusCode();
            using HttpResponseMessage deleteParentResponse = await _client.DeleteAsync($"{Routes.V1.Layouts}/nodes/{parent.Id}");
            deleteParentResponse.EnsureSuccessStatusCode();
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage restoreResponse = await _client.PostAsJsonAsync(
                $"{Routes.V1.Files}/{file.Id}/restore",
                new RestoreItemRequestDto { CreateMissingParents = true });
            restoreResponse.EnsureSuccessStatusCode();

            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);
            SyncChangeDto parentCreated = response.Changes.Single(x =>
                x.Kind == SyncChangeKind.FolderCreated && x.Name == "file-restore-created-parent");
            SyncChangeDto fileRestored = response.Changes.Single(x => x.ItemId == file.Id);

            Assert.Multiple(() =>
            {
                Assert.That(parentCreated.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(fileRestored.Kind, Is.EqualTo(SyncChangeKind.FileRestored));
                Assert.That(fileRestored.ParentNodeId, Is.EqualTo(parentCreated.ItemId));
                Assert.That(fileRestored.Name, Is.EqualTo("sync-restored-with-parent.txt"));
            });
        }

    }
}
