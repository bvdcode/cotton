// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task Hls_Master_Playlist_Exposes_All_Renditions_For_Transcodable_Share()
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeFileManifestDto file = await UploadTextFileAsync(
                root!,
                "transcodable.avi",
                "video payload",
                contentType: "video/x-msvideo");
            byte[] previewBytes = CreateWebpSignatureBytes("video preview");
            await StoreSmallPreviewAsync(file.Id, Hasher.HashData(previewBytes), previewBytes);

            using HttpResponseMessage linkResponse = await _client.GetAsync(
                $"/api/v1/files/{file.Id}/download-link");
            linkResponse.EnsureSuccessStatusCode();
            string downloadLink = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            string shareToken = ExtractToken(downloadLink);
            _client.DefaultRequestHeaders.Authorization = null;

            using HttpResponseMessage response = await _client.GetAsync(
                $"/api/v1/files/{file.Id}/hls/master.m3u8?token={shareToken}");
            response.EnsureSuccessStatusCode();
            string playlist = await response.Content.ReadAsStringAsync();

            Assert.Multiple(() =>
            {
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/vnd.apple.mpegurl"));
                Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
                Assert.That(playlist, Does.Contain("quality=source"));
                Assert.That(playlist, Does.Contain("quality=high"));
                Assert.That(playlist, Does.Contain("quality=medium"));
                Assert.That(playlist, Does.Contain("quality=low"));
            });
        }

        [Test]
        public async Task File_Versions_List_Download_And_Restore_Previous_Content()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "versioned.txt", "first", new Dictionary<string, string>
            {
                ["originalContentType"] = "text/plain",
            });
            file = await UpdateTextFileAsync(file, root!, "second");
            file = await UpdateTextFileAsync(file, root!, "third");

            List<FileVersionDto> versions = await GetVersionsAsync(file.Id);
            Assert.That(versions, Has.Count.EqualTo(3));
            Assert.That(versions[0].IsCurrent, Is.True);
            Assert.That(versions[0].VersionNumber, Is.EqualTo(3));

            FileVersionDto original = versions.Single(x => x.IsOriginal);
            Assert.Multiple(() =>
            {
                Assert.That(original.VersionNumber, Is.EqualTo(1));
                Assert.That(original.CanDelete, Is.False);
                Assert.That(versions.Single(x => x.VersionNumber == 2).CanDelete, Is.True);
            });

            string originalText = await DownloadVersionTextAsync(file.Id, original.Id);
            Assert.That(originalText, Is.EqualTo("first"));

            HttpResponseMessage restoreResponse = await _client.PostAsync($"/api/v1/files/{file.Id}/versions/{original.Id}/restore", null);
            restoreResponse.EnsureSuccessStatusCode();

            HttpResponseMessage currentLinkResponse = await _client.GetAsync($"/api/v1/files/{file.Id}/download-link");
            currentLinkResponse.EnsureSuccessStatusCode();
            string currentLink = (await currentLinkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            HttpResponseMessage currentDownload = await _client.GetAsync(currentLink);
            currentDownload.EnsureSuccessStatusCode();
            string restoredText = Encoding.UTF8.GetString(await currentDownload.Content.ReadAsByteArrayAsync());
            Assert.That(restoredText, Is.EqualTo("first"));

            List<FileVersionDto> versionsAfterRestore = await GetVersionsAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(versionsAfterRestore, Has.Count.EqualTo(4));
                Assert.That(versionsAfterRestore[0].IsCurrent, Is.True);
                Assert.That(versionsAfterRestore[0].VersionNumber, Is.EqualTo(4));
                Assert.That(versionsAfterRestore.Single(x => x.IsOriginal).Id, Is.EqualTo(original.Id));
            });

            file = await UpdateTextFileAsync(file, root!, "fourth");

            List<FileVersionDto> versionsAfterRestoreAndUpdate = await GetVersionsAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(versionsAfterRestoreAndUpdate, Has.Count.EqualTo(5));
                Assert.That(versionsAfterRestoreAndUpdate[0].IsCurrent, Is.True);
                Assert.That(versionsAfterRestoreAndUpdate[0].VersionNumber, Is.EqualTo(5));
            });
        }

        [Test]
        public async Task File_Versions_Restore_Rejects_When_Restored_Copy_Would_Exceed_Quota()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage quotaResponse = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-storage-quota-bytes",
                10L);
            quotaResponse.EnsureSuccessStatusCode();

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "restore-quota.txt", "123456");
            file = await UpdateTextFileAsync(file, root!, "x");

            List<FileVersionDto> versions = await GetVersionsAsync(file.Id);
            FileVersionDto original = versions.Single(x => x.IsOriginal);

            HttpResponseMessage restoreResponse = await _client.PostAsync($"/api/v1/files/{file.Id}/versions/{original.Id}/restore", null);
            Assert.That(restoreResponse.StatusCode, Is.EqualTo((HttpStatusCode)507));

            UserStorageQuotaDto? quota = await _client.GetFromJsonAsync<UserStorageQuotaDto>("/api/v1/users/me/storage-quota");
            Assert.That(quota, Is.Not.Null);
            Assert.That(quota!.UsedBytes, Is.EqualTo(7));
        }

        [Test]
        public async Task File_Versions_Retention_Keeps_Original_And_Prunes_Oldest_Middle()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "retention.txt", "v0");
            for (int i = 1; i <= 12; i++)
            {
                file = await UpdateTextFileAsync(file, root!, "v" + i);
            }

            List<FileVersionDto> versions = await GetVersionsAsync(file.Id);
            FileVersionDto original = versions.Single(x => x.IsOriginal);

            Assert.Multiple(() =>
            {
                Assert.That(versions, Has.Count.EqualTo(11));
                Assert.That(versions.Count(x => !x.IsCurrent), Is.EqualTo(10));
                Assert.That(versions[0].IsCurrent, Is.True);
                Assert.That(original.CanDelete, Is.False);
            });

            string originalText = await DownloadVersionTextAsync(file.Id, original.Id);
            Assert.That(originalText, Is.EqualTo("v0"));
        }

        [Test]
        public async Task File_Versions_Delete_Allows_NonOriginal_Only()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "retained.txt", "one");
            file = await UpdateTextFileAsync(file, root!, "two");
            file = await UpdateTextFileAsync(file, root!, "three");

            List<FileVersionDto> versions = await GetVersionsAsync(file.Id);
            FileVersionDto original = versions.Single(x => x.IsOriginal);
            FileVersionDto middle = versions.Single(x => !x.IsCurrent && !x.IsOriginal);

            Guid[] versionWrapperNodeIds = await DbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == original.Id || x.Id == middle.Id)
                .Select(x => x.NodeId)
                .ToArrayAsync();

            NodeDto? trashRoot = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver?nodeType=Trash");
            Assert.That(trashRoot, Is.Not.Null);
            NodeContentDto? directTrashContent = await _client.GetFromJsonAsync<NodeContentDto>(
                $"/api/v1/layouts/nodes/{trashRoot!.Id}/children?nodeType=Trash");
            NodeContentDto? trashContent = await _client.GetFromJsonAsync<NodeContentDto>(
                $"/api/v1/layouts/nodes/{trashRoot.Id}/children?nodeType=Trash&depth=1");
            Assert.That(directTrashContent, Is.Not.Null);
            Assert.That(trashContent, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(directTrashContent!.Nodes.Select(x => x.Id).Intersect(versionWrapperNodeIds), Is.Empty);
                Assert.That(trashContent!.Files.Select(x => x.Id), Does.Not.Contain(original.Id));
                Assert.That(trashContent.Files.Select(x => x.Id), Does.Not.Contain(middle.Id));
            });

            HttpResponseMessage directDeleteOriginal = await _client.DeleteAsync($"/api/v1/files/{original.Id}?skipTrash=true");
            Assert.That(directDeleteOriginal.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            HttpResponseMessage deleteOriginal = await _client.DeleteAsync($"/api/v1/files/{file.Id}/versions/{original.Id}");
            Assert.That(deleteOriginal.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            HttpResponseMessage deleteMiddle = await _client.DeleteAsync($"/api/v1/files/{file.Id}/versions/{middle.Id}");
            deleteMiddle.EnsureSuccessStatusCode();

            List<FileVersionDto> remaining = await GetVersionsAsync(file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(remaining.Select(x => x.Id), Does.Not.Contain(middle.Id));
                Assert.That(remaining.Select(x => x.Id), Does.Contain(original.Id));
                Assert.That(remaining.Single(x => x.IsOriginal).CanDelete, Is.False);
            });
        }

        [Test]
        public async Task Folder_Permanent_Delete_Removes_File_Version_Lineages()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto folder = await CreateFolderAsync(root!.Id, "versioned-folder");
            NodeFileManifestDto file = await UploadTextFileAsync(folder, "versioned-in-folder.txt", "one");
            file = await UpdateTextFileAsync(file, folder, "two");

            List<FileVersionDto> versions = await GetVersionsAsync(file.Id);
            Assert.That(versions, Has.Count.EqualTo(2));

            Guid[] versionWrapperNodeIds = await DbContext.NodeFiles
                .AsNoTracking()
                .Where(x => x.OriginalNodeFileId == file.Id && x.Id != file.Id)
                .Select(x => x.NodeId)
                .ToArrayAsync();
            Assert.That(versionWrapperNodeIds, Is.Not.Empty);

            HttpResponseMessage delete = await _client.DeleteAsync($"/api/v1/layouts/nodes/{folder.Id}?skipTrash=true");
            delete.EnsureSuccessStatusCode();

            DbContext.ChangeTracker.Clear();
            bool lineageExists = await DbContext.NodeFiles
                .AnyAsync(x => x.Id == file.Id || x.OriginalNodeFileId == file.Id);
            bool wrapperExists = await DbContext.Nodes
                .AnyAsync(x => versionWrapperNodeIds.Contains(x.Id));

            Assert.Multiple(() =>
            {
                Assert.That(lineageExists, Is.False);
                Assert.That(wrapperExists, Is.False);
            });
        }

    }
}
