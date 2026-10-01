// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class SyncChangesMetadataWebDavAndVersionsTests : SyncChangesTestBase
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
        public async Task WebDavPutFile_PersistsComputedContentHash()
        {
            await SignInAsync();
            await UseWebDavBasicAuthAsync();
            const string body = "webdav-hash-body";
            using HttpResponseMessage response = await SendWebDavPutAsync(
                "/api/v1/webdav/webdav-hash.txt",
                body);
            response.EnsureSuccessStatusCode();

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await dbContext.NodeFiles
                .Where(file => file.Name == "webdav-hash.txt")
                .Select(file => file.FileManifest)
                .SingleAsync();
            byte[] expectedHash = Hasher.HashData(Encoding.UTF8.GetBytes(body));

            Assert.Multiple(() =>
            {
                Assert.That(manifest.ProposedContentHash, Is.EqualTo(expectedHash));
                Assert.That(manifest.ComputedContentHash, Is.EqualTo(expectedHash));
            });
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                .RequireValid(dbContext, manifest, "test.webdav-computed-hash");
        }

        [Test]
        public async Task WebDavPutFile_ValidatesReusedUncomputedManifest()
        {
            await SignInAsync();
            NodeDto root = await GetRootAsync();
            const string body = "webdav-reused-body";
            NodeFileManifestDto created = await CreateFileAsync(root.Id, "existing-webdav.txt", body);

            await using (AsyncServiceScope beforeScope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext before = beforeScope.ServiceProvider.GetRequiredService<CottonDbContext>();
                FileManifest manifest = await before.FileManifests.SingleAsync(item => item.Id == created.FileManifestId);
                Assert.That(manifest.ComputedContentHash, Is.Null);
            }

            await UseWebDavBasicAuthAsync();
            using HttpResponseMessage response = await SendWebDavPutAsync(
                "/api/v1/webdav/reused-webdav.txt",
                body);
            response.EnsureSuccessStatusCode();

            await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest reused = await dbContext.FileManifests.SingleAsync(item => item.Id == created.FileManifestId);
            Assert.That(reused.ComputedContentHash, Is.EqualTo(Hasher.HashData(Encoding.UTF8.GetBytes(body))));
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                .RequireValid(dbContext, reused, "test.webdav-reused-hash");
        }

        [Test]
        public async Task WebDavPutFile_DoesNotValidateDifferentChunksWithSameProposedHash()
        {
            await SignInAsync();
            NodeDto root = await GetRootAsync();
            const string actualBody = "webdav-actual-body";
            const string otherBody = "different-contents";
            string otherChunkHash = await UploadChunkAsync(otherBody);
            byte[] actualHash = Hasher.HashData(Encoding.UTF8.GetBytes(actualBody));

            using HttpResponseMessage createResponse = await _client!.PostAsJsonAsync(
                $"{Routes.V1.Files}/from-chunks",
                new CreateFileFromChunksRequestDto
                {
                    ChunkHashes = [otherChunkHash],
                    Name = "unverified-webdav.txt",
                    ContentType = "text/plain",
                    Hash = Hasher.ToHexStringHash(actualHash),
                    NodeId = root.Id,
                });
            createResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto? created = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(created, Is.Not.Null);

            await UseWebDavBasicAuthAsync();
            using HttpResponseMessage putResponse = await SendWebDavPutAsync(
                "/api/v1/webdav/second-webdav.txt",
                actualBody);
            putResponse.EnsureSuccessStatusCode();

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            FileManifest manifest = await dbContext.FileManifests.SingleAsync(item => item.Id == created!.FileManifestId);
            Assert.Multiple(() =>
            {
                Assert.That(manifest.ProposedContentHash, Is.EqualTo(actualHash));
                Assert.That(manifest.ComputedContentHash, Is.Null);
            });
            scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                .RequireValid(dbContext, manifest, "test.webdav-different-chunks");
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

        private async Task<NodeFileManifestDto> UpdateFileContentAsync(Guid nodeFileId, Guid nodeId, string name, string body)
        {
            string hash = await UploadChunkAsync(body);
            using HttpResponseMessage response = await _client!.PatchAsJsonAsync(
                $"{Routes.V1.Files}/{nodeFileId}/update-content",
                new CreateFileFromChunksRequestDto
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

        private async Task UseWebDavBasicAuthAsync()
        {
            string webDavToken = await _client!.GetStringAsync("/api/v1/auth/webdav/token");
            Assert.That(webDavToken, Is.Not.Empty);
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{webDavToken}")));
        }

        private async Task<HttpResponseMessage> SendWebDavPutAsync(string path, string body)
        {
            using StringContent content = new StringContent(body, Encoding.UTF8, "text/plain");
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = content,
            };

            return await _client!.SendAsync(request);
        }

        private async Task<HttpResponseMessage> SendWebDavMkColAsync(string path)
        {
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("MKCOL"), path);
            return await _client!.SendAsync(request);
        }

        private async Task<HttpResponseMessage> SendWebDavMoveAsync(string sourcePath, string destinationPath)
        {
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("MOVE"), sourcePath);
            request.Headers.Add("Destination", destinationPath);
            request.Headers.Add("Overwrite", "F");
            return await _client!.SendAsync(request);
        }

        private async Task<HttpResponseMessage> SendWebDavCopyAsync(string sourcePath, string destinationPath)
        {
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("COPY"), sourcePath);
            request.Headers.Add("Destination", destinationPath);
            request.Headers.Add("Overwrite", "F");
            return await _client!.SendAsync(request);
        }
    }
}
