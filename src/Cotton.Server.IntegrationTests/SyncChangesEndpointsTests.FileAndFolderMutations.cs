// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class SyncChangesEndpointsTests
    {
        [Test]
        public async Task RenameFolder_StagesFolderRenamedChangeWithParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "sync-before-rename");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage renameResponse = await _client!.PatchAsJsonAsync(
                $"{Routes.V1.Layouts}/nodes/{folder.Id}/rename",
                new RenameNodeRequestDto { Name = "sync-after-rename" });
            renameResponse.EnsureSuccessStatusCode();

            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);
            SyncChangeDto change = response.Changes.Single(x => x.ItemId == folder.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FolderRenamed));
                Assert.That(change.LayoutId, Is.EqualTo(folder.LayoutId));
                Assert.That(change.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(change.Name, Is.EqualTo("sync-after-rename"));
            });
        }

        [Test]
        public async Task CreateFolder_StagesFolderCreatedChangeWithParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;
            NodeDto folder = await CreateFolderAsync(root.Id, "sync-created-folder");

            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);
            SyncChangeDto change = response.Changes.Single(x => x.ItemId == folder.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FolderCreated));
                Assert.That(change.LayoutId, Is.EqualTo(folder.LayoutId));
                Assert.That(change.ParentNodeId, Is.EqualTo(root.Id));
                Assert.That(change.Name, Is.EqualTo("sync-created-folder"));
            });
        }

        [Test]
        public async Task CreateFile_StagesFileCreatedChangeWithParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "file-create-parent");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            NodeFileManifestDto file = await CreateFileAsync(folder.Id, "sync-created-file.txt", "created-body");

            SyncChangeDto change = await GetSingleChangeAsync(cursor, file.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileCreated));
                Assert.That(change.ParentNodeId, Is.EqualTo(folder.Id));
                Assert.That(change.FileManifestId, Is.EqualTo(file.FileManifestId));
                Assert.That(change.Name, Is.EqualTo("sync-created-file.txt"));
            });
        }

        [Test]
        public async Task RenameFile_StagesFileRenamedChangeWithParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "file-rename-parent");
            NodeFileManifestDto file = await CreateFileAsync(folder.Id, "sync-before-rename.txt", "rename-body");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage renameResponse = await _client!.PatchAsJsonAsync(
                $"{Routes.V1.Files}/{file.Id}/rename",
                new RenameFileRequestDto { Name = "sync-after-rename.txt" });
            renameResponse.EnsureSuccessStatusCode();

            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);
            SyncChangeDto change = response.Changes.Single(x => x.ItemId == file.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileRenamed));
                Assert.That(change.ParentNodeId, Is.EqualTo(folder.Id));
                Assert.That(change.FileManifestId, Is.EqualTo(file.FileManifestId));
                Assert.That(change.Name, Is.EqualTo("sync-after-rename.txt"));
            });
        }

        [Test]
        public async Task MoveFile_StagesFileMovedChangeWithPreviousParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto source = await CreateFolderAsync(root.Id, "move-file-source");
            NodeDto target = await CreateFolderAsync(root.Id, "move-file-target");
            NodeFileManifestDto file = await CreateFileAsync(source.Id, "sync-moved-file.txt", "moved-body");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage moveResponse = await _client!.PatchAsJsonAsync(
                $"{Routes.V1.Files}/{file.Id}/move",
                new MoveFileRequestDto { ParentId = target.Id });
            moveResponse.EnsureSuccessStatusCode();

            SyncChangeDto change = await GetSingleChangeAsync(cursor, file.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileMoved));
                Assert.That(change.ParentNodeId, Is.EqualTo(target.Id));
                Assert.That(change.PreviousParentNodeId, Is.EqualTo(source.Id));
                Assert.That(change.Name, Is.EqualTo("sync-moved-file.txt"));
            });
        }

        [Test]
        public async Task MoveFolder_StagesFolderMovedChangeWithPreviousParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto source = await CreateFolderAsync(root.Id, "move-folder-source");
            NodeDto target = await CreateFolderAsync(root.Id, "move-folder-target");
            NodeDto folder = await CreateFolderAsync(source.Id, "sync-moved-folder");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage moveResponse = await _client!.PatchAsJsonAsync(
                $"{Routes.V1.Layouts}/nodes/{folder.Id}/move",
                new MoveNodeRequestDto { ParentId = target.Id });
            moveResponse.EnsureSuccessStatusCode();

            SyncChangeDto change = await GetSingleChangeAsync(cursor, folder.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FolderMoved));
                Assert.That(change.ParentNodeId, Is.EqualTo(target.Id));
                Assert.That(change.PreviousParentNodeId, Is.EqualTo(source.Id));
                Assert.That(change.Name, Is.EqualTo("sync-moved-folder"));
            });
        }

        [Test]
        public async Task DeleteFile_StagesFileDeletedChangeWithOriginalParentNodeId()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "delete-file-parent");
            NodeFileManifestDto file = await CreateFileAsync(folder.Id, "sync-deleted-file.txt", "deleted-body");
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage deleteResponse = await _client!.DeleteAsync($"{Routes.V1.Files}/{file.Id}");
            deleteResponse.EnsureSuccessStatusCode();

            SyncChangeDto change = await GetSingleChangeAsync(cursor, file.Id);

            Assert.Multiple(() =>
            {
                Assert.That(change.Kind, Is.EqualTo(SyncChangeKind.FileDeleted));
                Assert.That(change.ParentNodeId, Is.EqualTo(folder.Id));
                Assert.That(change.FileManifestId, Is.EqualTo(file.FileManifestId));
                Assert.That(change.Name, Is.EqualTo("sync-deleted-file.txt"));
            });
        }

        [Test]
        public async Task PermanentDeleteFileFromTrash_DoesNotStageSecondFileDeletedChange()
        {
            await SignInAsync();

            NodeDto root = await GetRootAsync();
            NodeFileManifestDto file = await CreateFileAsync(root.Id, "trash-cleanup-file.txt", "deleted-body");

            using HttpResponseMessage trashResponse = await _client!.DeleteAsync($"{Routes.V1.Files}/{file.Id}");
            trashResponse.EnsureSuccessStatusCode();
            long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

            using HttpResponseMessage permanentDeleteResponse = await _client.DeleteAsync(
                $"{Routes.V1.Files}/{file.Id}?skipTrash=true");
            permanentDeleteResponse.EnsureSuccessStatusCode();

            SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 10);

            Assert.That(response.Changes.Select(x => x.ItemId), Does.Not.Contain(file.Id));
        }

    }
}
