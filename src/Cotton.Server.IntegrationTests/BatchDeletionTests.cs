// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Integrity;
using Cotton.Database.Models.Enums;
using Cotton.Files;
using Cotton.Models.Enums;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using System.Net;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class BatchDeletionTests : FileEndpointTestBase
    {
        [Test]
        public async Task MoveToTrash_PreservesPathsAndMetadataAndExpiresSignedShares()
        {
            NodeDto root = await AuthenticateAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "documents");
            NodeDto child = await CreateFolderAsync(folder.Id, "nested");
            NodeFileManifestDto nested = await UploadTextFileAsync(child, "nested.txt", "nested");
            NodeFileManifestDto file = await UploadTextFileAsync(root, "plain.txt", "plain", new() { ["custom"] = "kept" });
            await _client!.GetStringAsync($"/api/v1/files/{nested.Id}/download-link");
            await _client.GetStringAsync($"/api/v1/files/{file.Id}/download-link");
            List<BatchItemRequestDto> items = [Folder(folder.Id), Folder(child.Id), File(nested.Id), File(file.Id)];

            List<BatchItemResultDto> results = await DeleteAsync(items);
            Assert.That(results.All(x => x.Deleted && !x.Failed), Is.True);
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Node moved = await db.Nodes.SingleAsync(x => x.Id == folder.Id);
            Node movedChild = await db.Nodes.SingleAsync(x => x.Id == child.Id);
            NodeFile movedFile = await db.NodeFiles.SingleAsync(x => x.Id == file.Id);
            Assert.Multiple(() =>
            {
                Assert.That(moved.Type, Is.EqualTo(NodeType.Trash));
                Assert.That(movedChild.Type, Is.EqualTo(NodeType.Trash));
                Assert.That(movedChild.ParentId, Is.EqualTo(moved.Id));
                Assert.That(TrashParentMetadata.Read(moved.Metadata)!.Select(x => x.Id), Is.EqualTo(new[] { root.Id }));
                Assert.That(movedFile.Metadata!["custom"], Is.EqualTo("kept"));
            });
            List<DownloadToken> tokens = await db.DownloadTokens.ToListAsync();
            IDatabaseIntegrityVerifier verifier = scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>();
            Assert.That(tokens, Has.Count.EqualTo(2));
            foreach (DownloadToken token in tokens)
            {
                Assert.That(token.ExpiresAt, Is.LessThanOrEqualTo(DateTime.UtcNow));
                verifier.RequireValid(db, token, "trash-test");
            }
            Guid[] changedIds = await db.SyncChanges.Where(x => x.Kind == SyncChangeKind.FolderDeleted || x.Kind == SyncChangeKind.FileDeleted)
                .Select(x => x.ItemId).ToArrayAsync();
            Assert.That(changedIds, Is.EquivalentTo(new[] { folder.Id, file.Id }));

            (await _client.PostAsync($"/api/v1/layouts/nodes/{folder.Id}/restore", null)).EnsureSuccessStatusCode();
            (await _client.PostAsync($"/api/v1/files/{file.Id}/restore", null)).EnsureSuccessStatusCode();
        }

        [Test]
        public async Task PermanentDeletion_RemovesTreeAndVersionsAndReleasesQuota()
        {
            NodeDto root = await AuthenticateAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "documents");
            NodeDto child = await CreateFolderAsync(folder.Id, "nested");
            NodeFileManifestDto file = await UploadTextFileAsync(child, "versioned.txt", "first");
            file = await UpdateTextFileAsync(file, child, "second");
            await _client!.GetStringAsync($"/api/v1/files/{file.Id}/download-link");
            UserStorageQuotaDto? before = await _client.GetFromJsonAsync<UserStorageQuotaDto>("/api/v1/users/me/storage-quota");
            Assert.That(before!.UsedBytes, Is.GreaterThan(0));

            List<BatchItemResultDto> results = await DeleteAsync([Folder(folder.Id), Folder(child.Id), File(file.Id), Folder(folder.Id)], true);
            Assert.That(results.All(x => x.Deleted && !x.Failed), Is.True);
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(await db.Nodes.AnyAsync(x => x.Id == folder.Id || x.Id == child.Id), Is.False);
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == file.Id || x.OriginalNodeFileId == file.Id), Is.False);
            Assert.That(await db.DownloadTokens.AnyAsync(), Is.False);
            Assert.That(await db.Nodes.CountAsync(x => x.Type == NodeType.Trash && x.ParentId != null), Is.Zero);
            UserStorageQuotaDto? after = await _client.GetFromJsonAsync<UserStorageQuotaDto>("/api/v1/users/me/storage-quota");
            Assert.That(after!.UsedBytes, Is.Zero);
        }

        [Test]
        public async Task Delete_InvalidItemsFailWithoutBlockingOwnedItems()
        {
            NodeDto root = await AuthenticateAsync();
            NodeFileManifestDto file = await UploadTextFileAsync(root, "plain.txt", "plain");
            Guid missingId = Guid.NewGuid();
            List<BatchItemResultDto> results = await DeleteAsync([Folder(root.Id), File(missingId), File(file.Id)]);
            Assert.Multiple(() =>
            {
                Assert.That(results.Select(x => x.Id), Is.EqualTo(new[] { root.Id, missingId, file.Id }));
                Assert.That(results.Select(x => x.Deleted), Is.EqualTo(new[] { false, false, true }));
                Assert.That(results.Select(x => x.Failed), Is.EqualTo(new[] { true, true, false }));
            });
        }

        [Test]
        public async Task PermanentDeletion_ProtectsOriginalVersionAndVersionContainers()
        {
            NodeDto root = await AuthenticateAsync();
            NodeFileManifestDto file = await UploadTextFileAsync(root, "versioned.txt", "first");
            file = await UpdateTextFileAsync(file, root, "second");
            file = await UpdateTextFileAsync(file, root, "third");
            List<FileVersionDto> versions = await GetVersionsAsync(file.Id);
            FileVersionDto original = versions.Single(x => x.IsOriginal);
            FileVersionDto other = versions.Single(x => !x.IsCurrent && !x.IsOriginal);
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Guid wrapperId = await db.NodeFiles.Where(x => x.Id == original.Id).Select(x => x.NodeId).SingleAsync();

            List<BatchItemResultDto> results = await DeleteAsync([File(original.Id), Folder(wrapperId), File(other.Id)], true);
            Assert.That(results.Select(x => x.Deleted), Is.EqualTo(new[] { false, false, true }));
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == original.Id), Is.True);
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == other.Id), Is.False);
        }

        [Test]
        public async Task Delete_Handles65FilesInOneRequestAndCanEmptyTheirWrappers()
        {
            NodeDto root = await AuthenticateAsync();
            NodeFileManifestDto seed = await UploadTextFileAsync(root, "seed.txt", "same");
            Guid[] ids;
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                Node parent = await db.Nodes.SingleAsync(x => x.Id == root.Id);
                NodeFile template = await db.NodeFiles.SingleAsync(x => x.Id == seed.Id);
                List<NodeFile> copies = Enumerable.Range(1, 64).Select(x => template.CopyTo(parent, $"file-{x}.txt")).ToList();
                db.NodeFiles.AddRange(copies);
                await db.SaveChangesAsync();
                ids = [seed.Id, .. copies.Select(x => x.Id)];
            }
            List<BatchItemResultDto> moved = await DeleteAsync(ids.Select(File).ToList());
            Assert.That(moved, Has.Count.EqualTo(65));
            Assert.That(moved.All(x => x.Deleted), Is.True);
            await using AsyncServiceScope readScope = _factory!.Services.CreateAsyncScope();
            CottonDbContext readDb = readScope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Guid[] wrappers = await readDb.NodeFiles.Where(x => ids.Contains(x.Id)).Select(x => x.NodeId).ToArrayAsync();
            Assert.That(wrappers.Distinct().Count(), Is.EqualTo(65));
            List<BatchItemResultDto> removed = await DeleteAsync(wrappers.Select(Folder).ToList(), true);
            Assert.That(removed.All(x => x.Deleted), Is.True);
            Assert.That(await readDb.NodeFiles.AnyAsync(x => ids.Contains(x.Id)), Is.False);
        }

        [Test]
        public async Task Delete_DoesNotTouchAnotherOwnersFile()
        {
            NodeDto root = await AuthenticateAsync();
            NodeFileManifestDto foreign = await UploadTextFileAsync(root, "foreign.txt", "foreign");
            Guid ownerId = foreign.OwnerId;
            using HttpResponseMessage created = await _client!.PostAsJsonAsync("/api/v1/users",
                new Cotton.Server.Handlers.Users.AdminCreateUserRequest("other-user", "other@example.com", "testpassword", EasyExtensions.Models.Enums.UserRole.User));
            created.EnsureSuccessStatusCode();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync("other-user"));

            List<BatchItemResultDto> results = await DeleteAsync([File(foreign.Id)], true);
            Assert.That(results.Single().Failed, Is.True);
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(await db.NodeFiles.AnyAsync(x => x.Id == foreign.Id && x.OwnerId == ownerId), Is.True);
        }

        [Test]
        public async Task PermanentDeletion_RollsBackAllItemsAndSharesOnSaveFailure()
        {
            NodeDto root = await AuthenticateAsync();
            NodeFileManifestDto first = await UploadTextFileAsync(root, "first.txt", "first");
            NodeFileManifestDto second = await UploadTextFileAsync(root, "second.txt", "second");
            await _client!.GetStringAsync($"/api/v1/files/{first.Id}/download-link");
            Dictionary<string, string?> overrides = new()
            {
                ["DatabaseSettings:Host"] = TestPostgresHost,
                ["DatabaseSettings:Port"] = TestPostgresPort.ToString(),
                ["DatabaseSettings:Database"] = CurrentDatabaseName,
                ["DatabaseSettings:Username"] = TestPostgresUsername,
                ["DatabaseSettings:Password"] = TestPostgresPassword,
                ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
                ["MasterEncryptionKeyId"] = "1",
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
            };
            using TestAppFactory failing = new(overrides, services => services.AddDbContext<CottonDbContext>(
                (_, options) => options.AddInterceptors(new RejectDeletionSaveInterceptor())));
            using HttpClient client = failing.CreateClient();
            client.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;

            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/items/delete",
                new BatchItemsRequestDto { Items = [File(first.Id), File(second.Id)], SkipTrash = true });
            Assert.That(response.IsSuccessStatusCode, Is.False);
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(await db.NodeFiles.CountAsync(x => x.Id == first.Id || x.Id == second.Id), Is.EqualTo(2));
            Assert.That(await db.DownloadTokens.CountAsync(x => x.NodeFileId == first.Id), Is.EqualTo(1));
            Assert.That(await db.SyncChanges.AnyAsync(x => x.Kind == SyncChangeKind.FileDeleted), Is.False);
        }

        [Test]
        public async Task Restore_RejectsOversizedBatchBeforeChangingAnything()
        {
            await AuthenticateAsync();
            using HttpResponseMessage response = await _client!.PostAsJsonAsync("/api/v1/items/restore",
                new BatchItemsRequestDto { Items = Enumerable.Range(0, BatchItemLimits.RestoreCount + 1).Select(_ => File(Guid.NewGuid())).ToList() });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        private async Task<NodeDto> AuthenticateAsync()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            return (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
        }

        private async Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
        {
            using HttpResponseMessage response = await _client!.PutAsJsonAsync("/api/v1/layouts/nodes",
                new CreateNodeRequestDto { ParentId = parentId, Name = name });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<NodeDto>())!;
        }

        private async Task<List<BatchItemResultDto>> DeleteAsync(List<BatchItemRequestDto> items, bool skipTrash = false)
        {
            using HttpResponseMessage response = await _client!.PostAsJsonAsync("/api/v1/items/delete",
                new BatchItemsRequestDto { Items = items, SkipTrash = skipTrash });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<BatchItemResultDto>>())!;
        }

        private static BatchItemRequestDto File(Guid id) => new() { Id = id, Kind = BatchItemKind.File };

        private static BatchItemRequestDto Folder(Guid id) => new() { Id = id, Kind = BatchItemKind.Folder };
    }
}
