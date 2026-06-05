// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;

namespace Cotton.Server.IntegrationTests;

[NonParallelizable]
public class SyncChangesEndpointsTests : IntegrationTestBase
{
    private const string Username = "testuser";
    private const string Password = "testpassword";

    private TestAppFactory? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        ResetSettingsProviderCaches();

        IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        creator.Create();

        _factory = new TestAppFactory(CreateOverrides());
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
        ResetSettingsProviderCaches();
    }

    [Test]
    public async Task GetChanges_WhenFeedIsEmpty_ReturnsEmptyPage()
    {
        await SignInAsync();

        SyncChangesResponseDto response = await GetChangesAsync(since: 0, limit: 10);

        Assert.Multiple(() =>
        {
            Assert.That(response.SinceCursor, Is.EqualTo(0));
            Assert.That(response.NextCursor, Is.EqualTo(0));
            Assert.That(response.HasMore, Is.False);
            Assert.That(response.Changes, Is.Empty);
        });
    }

    [Test]
    public async Task GetChanges_ReturnsOrderedCurrentUserPageAfterCursor()
    {
        await SignInAsync();
        Guid ownerId = await GetUserIdAsync(Username);
        await CreateUserAsync("otheruser", "otherpass");
        Guid otherOwnerId = await GetUserIdAsync("otheruser");

        long firstOwnerChangeId = await AddSyncChangeAsync(ownerId, "ignored-before-cursor");
        await AddSyncChangeAsync(otherOwnerId, "other-user");
        long includedChangeId = await AddSyncChangeAsync(ownerId, "included");
        await AddSyncChangeAsync(ownerId, "next-page");

        SyncChangesResponseDto response = await GetChangesAsync(since: firstOwnerChangeId, limit: 1);

        Assert.Multiple(() =>
        {
            Assert.That(response.SinceCursor, Is.EqualTo(firstOwnerChangeId));
            Assert.That(response.NextCursor, Is.EqualTo(includedChangeId));
            Assert.That(response.HasMore, Is.True);
            Assert.That(response.Changes, Has.Count.EqualTo(1));
            Assert.That(response.Changes[0].Id, Is.EqualTo(includedChangeId));
            Assert.That(response.Changes[0].Name, Is.EqualTo("included"));
            Assert.That(response.Changes[0].Kind, Is.EqualTo(SyncChangeKind.FileCreated));
        });
    }

    [Test]
    public async Task GetChanges_WhenCursorIsNegative_ReturnsBadRequest()
    {
        await SignInAsync();

        using HttpResponseMessage response = await _client!.GetAsync($"{Routes.V1.Sync}/changes?since=-1&limit=10");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetChanges_WhenCursorIsOlderThanRetainedFeed_ReturnsExpiredCursor()
    {
        await SignInAsync();
        Guid ownerId = await GetUserIdAsync(Username);

        long expiredId = await AddSyncChangeAsync(ownerId, "expired");
        long retainedId = await AddSyncChangeAsync(ownerId, "retained");
        await DeleteSyncChangeAsync(expiredId);

        SyncChangesResponseDto response = await GetChangesAsync(since: 0, limit: 10);

        Assert.Multiple(() =>
        {
            Assert.That(response.CursorExpired, Is.True);
            Assert.That(response.SinceCursor, Is.EqualTo(0));
            Assert.That(response.NextCursor, Is.EqualTo(0));
            Assert.That(response.EarliestAvailableCursor, Is.EqualTo(retainedId - 1));
            Assert.That(response.Changes, Is.Empty);
        });
    }

    [Test]
    public async Task RetentionJob_KeepsNewestExpiredChangeAsCursorMarker()
    {
        await SignInAsync();
        Guid ownerId = await GetUserIdAsync(Username);

        DateTime cutoff = DateTime.UtcNow.AddDays(-365);
        long oldestExpiredId = await AddSyncChangeAsync(ownerId, "oldest-expired");
        long markerId = await AddSyncChangeAsync(ownerId, "cursor-marker");
        await SetSyncChangeCreatedAtAsync(oldestExpiredId, cutoff.AddDays(-2));
        await SetSyncChangeCreatedAtAsync(markerId, cutoff.AddDays(-1));

        int deletedCount = await RunSyncRetentionAsync(cutoff);

        List<long> remainingIds = await GetSyncChangeIdsAsync(ownerId);
        SyncChangesResponseDto response = await GetChangesAsync(since: 0, limit: 10);

        Assert.Multiple(() =>
        {
            Assert.That(deletedCount, Is.EqualTo(1));
            Assert.That(remainingIds, Is.EqualTo(new[] { markerId }));
            Assert.That(response.CursorExpired, Is.True);
            Assert.That(response.EarliestAvailableCursor, Is.EqualTo(markerId - 1));
            Assert.That(response.Changes, Is.Empty);
        });
    }

    [Test]
    public async Task RenameFolder_StagesFolderRenamedChangeWithParentNodeId()
    {
        await SignInAsync();

        NodeDto root = await GetRootAsync();
        NodeDto folder = await CreateFolderAsync(root.Id, "sync-before-rename");
        long cursor = (await GetChangesAsync(since: 0, limit: 100)).NextCursor;

        using HttpResponseMessage renameResponse = await _client!.PatchAsJsonAsync(
            $"{Routes.V1.Layouts}/nodes/{folder.Id}/rename",
            new RenameNodeRequest { Name = "sync-after-rename" });
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
            new RenameFileRequest { Name = "sync-after-rename.txt" });
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
            new MoveFileRequest { ParentId = target.Id });
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
            new MoveNodeRequest { ParentId = target.Id });
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
            new RestoreItemRequest());
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
            new RestoreItemRequest());
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
            new RestoreItemRequest { CreateMissingParents = true });
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
            new RestoreItemRequest { CreateMissingParents = true });
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

        UseWebDavBasicAuth();
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

        UseWebDavBasicAuth();
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

        UseWebDavBasicAuth();
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

        UseWebDavBasicAuth();
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

    private Dictionary<string, string?> CreateOverrides()
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = TestPostgresHost,
            Port = TestPostgresPort,
            Database = CurrentDatabaseName,
            Username = TestPostgresUsername,
            Password = TestPostgresPassword,
        };

        return new Dictionary<string, string?>
        {
            ["DatabaseSettings:Host"] = csb.Host,
            ["DatabaseSettings:Port"] = csb.Port.ToString(),
            ["DatabaseSettings:Database"] = csb.Database,
            ["DatabaseSettings:Username"] = csb.Username,
            ["DatabaseSettings:Password"] = csb.Password,
            ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
            ["MasterEncryptionKeyId"] = "1",
            ["EncryptionThreads"] = "1",
            ["MaxChunkSizeBytes"] = "16777216",
            ["CipherChunkSizeBytes"] = "20971520",
            ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
        };
    }

    private async Task<string> SignInAsync(string username = Username, string password = Password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Routes.V1.Auth}/login")
        {
            Content = JsonContent.Create(new LoginRequestDto
            {
                Username = username,
                Password = password,
            }),
        };
        request.Headers.Add("X-Forwarded-For", "8.8.8.8");

        using HttpResponseMessage response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();

        TokenPairResponseDto? login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        Assert.That(login, Is.Not.Null);

        UseBearerAuth(login!.AccessToken);
        return login.AccessToken;
    }

    private async Task<Guid> GetUserIdAsync(string username)
    {
        using IServiceScope scope = _factory!.Services.CreateScope();
        CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

        User user = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(x => x.Username == username);

        return user.Id;
    }

    private async Task CreateUserAsync(string username, string password)
    {
        using HttpResponseMessage response = await _client!.PostAsJsonAsync(Routes.V1.Users, new
        {
            username,
            password,
            role = UserRole.User,
        });

        response.EnsureSuccessStatusCode();
    }

    private async Task<NodeDto> GetRootAsync()
    {
        NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>($"{Routes.V1.Layouts}/resolver");
        Assert.That(root, Is.Not.Null);
        return root!;
    }

    private async Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
    {
        using HttpResponseMessage response = await _client!.PutAsJsonAsync(
            $"{Routes.V1.Layouts}/nodes",
            new CreateNodeRequest { ParentId = parentId, Name = name });
        response.EnsureSuccessStatusCode();

        NodeDto? node = await response.Content.ReadFromJsonAsync<NodeDto>();
        Assert.That(node, Is.Not.Null);
        return node!;
    }

    private async Task<NodeFileManifestDto> CreateFileAsync(Guid nodeId, string name, string body)
    {
        string hash = await UploadChunkAsync(body);
        using HttpResponseMessage response = await _client!.PostAsJsonAsync(
            $"{Routes.V1.Files}/from-chunks",
            new CreateFileRequest
            {
                ChunkHashes = [hash],
                Name = name,
                ContentType = "application/octet-stream",
                Hash = hash,
                NodeId = nodeId,
            });
        response.EnsureSuccessStatusCode();

        NodeFileManifestDto? file = await response.Content.ReadFromJsonAsync<NodeFileManifestDto>();
        Assert.That(file, Is.Not.Null);
        return file!;
    }

    private async Task<NodeFileManifestDto> UpdateFileContentAsync(Guid nodeFileId, Guid nodeId, string name, string body)
    {
        string hash = await UploadChunkAsync(body);
        using HttpResponseMessage response = await _client!.PatchAsJsonAsync(
            $"{Routes.V1.Files}/{nodeFileId}/update-content",
            new CreateFileRequest
            {
                ChunkHashes = [hash],
                Name = name,
                ContentType = "application/octet-stream",
                Hash = hash,
                NodeId = nodeId,
            });
        response.EnsureSuccessStatusCode();

        NodeFileManifestDto? file = await response.Content.ReadFromJsonAsync<NodeFileManifestDto>();
        Assert.That(file, Is.Not.Null);
        return file!;
    }

    private async Task<List<FileVersionDto>> GetFileVersionsAsync(Guid nodeFileId)
    {
        List<FileVersionDto>? versions = await _client!.GetFromJsonAsync<List<FileVersionDto>>(
            $"{Routes.V1.Files}/{nodeFileId}/versions");

        Assert.That(versions, Is.Not.Null);
        return versions!;
    }

    private async Task<string> UploadChunkAsync(string body)
    {
        byte[] content = Encoding.UTF8.GetBytes(body);
        string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
        using var form = new MultipartFormDataContent
        {
            {
                new ByteArrayContent(content)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") },
                },
                "file",
                "chunk.bin"
            },
            { new StringContent(hash), "hash" },
        };

        using HttpResponseMessage response = await _client!.PostAsync(Routes.V1.Chunks, form);
        response.EnsureSuccessStatusCode();
        return hash;
    }

    private void UseBearerAuth(string accessToken)
    {
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private void UseWebDavBasicAuth()
    {
        _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}")));
    }

    private async Task<HttpResponseMessage> SendWebDavPutAsync(string path, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = content,
        };

        return await _client!.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendWebDavMkColAsync(string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod("MKCOL"), path);
        return await _client!.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendWebDavMoveAsync(string sourcePath, string destinationPath)
    {
        using var request = new HttpRequestMessage(new HttpMethod("MOVE"), sourcePath);
        request.Headers.Add("Destination", destinationPath);
        request.Headers.Add("Overwrite", "F");
        return await _client!.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendWebDavCopyAsync(string sourcePath, string destinationPath)
    {
        using var request = new HttpRequestMessage(new HttpMethod("COPY"), sourcePath);
        request.Headers.Add("Destination", destinationPath);
        request.Headers.Add("Overwrite", "F");
        return await _client!.SendAsync(request);
    }

    private async Task<long> AddSyncChangeAsync(Guid ownerId, string name)
    {
        using IServiceScope scope = _factory!.Services.CreateScope();
        CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

        var change = new SyncChange
        {
            OwnerId = ownerId,
            Kind = SyncChangeKind.FileCreated,
            LayoutId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            ParentNodeId = Guid.NewGuid(),
            Name = name,
        };

        dbContext.SyncChanges.Add(change);
        await dbContext.SaveChangesAsync();
        return change.Id;
    }

    private async Task DeleteSyncChangeAsync(long id)
    {
        using IServiceScope scope = _factory!.Services.CreateScope();
        CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

        await dbContext.SyncChanges
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync();
    }

    private async Task SetSyncChangeCreatedAtAsync(long id, DateTime createdAt)
    {
        using IServiceScope scope = _factory!.Services.CreateScope();
        CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

        await dbContext.SyncChanges
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x
                .SetProperty(change => change.CreatedAt, createdAt)
                .SetProperty(change => change.UpdatedAt, createdAt));
    }

    private async Task<int> RunSyncRetentionAsync(DateTime cutoff)
    {
        using IServiceScope scope = _factory!.Services.CreateScope();
        CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
        var job = new SyncChangeRetentionJob(
            dbContext,
            NullLogger<SyncChangeRetentionJob>.Instance);

        return await job.DeleteExpiredChangesAsync(cutoff, CancellationToken.None);
    }

    private async Task<List<long>> GetSyncChangeIdsAsync(Guid ownerId)
    {
        using IServiceScope scope = _factory!.Services.CreateScope();
        CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();

        return await dbContext.SyncChanges
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync();
    }

    private async Task<SyncChangesResponseDto> GetChangesAsync(long since, int limit)
    {
        SyncChangesResponseDto? response = await _client!.GetFromJsonAsync<SyncChangesResponseDto>(
            $"{Routes.V1.Sync}/changes?since={since}&limit={limit}");

        Assert.That(response, Is.Not.Null);
        return response!;
    }

    private async Task<SyncChangeDto> GetSingleChangeAsync(long cursor, Guid itemId)
    {
        SyncChangesResponseDto response = await GetChangesAsync(cursor, limit: 20);
        return response.Changes.Single(x => x.ItemId == itemId);
    }

    private static void ResetSettingsProviderCaches()
    {
        const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type settingsProviderType = typeof(SettingsProvider);

        settingsProviderType.GetField("_cache", Flags)?.SetValue(null, null);
        settingsProviderType.GetField("_isServerInitializedCache", Flags)?.SetValue(null, null);
        settingsProviderType.GetField("_serverHasUsersCache", Flags)?.SetValue(null, null);
    }
}
