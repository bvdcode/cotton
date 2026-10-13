// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;

namespace Cotton.Server.IntegrationTests
{
    public class CopyItemTests : MoveEndpointTestBase
    {
        [Test]
        public async Task CopyFile_PreservesContentAndMetadataWithIndependentIdentity()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto target = await CreateFolderAsync(root.Id, "target");
            NodeFileManifestDto source = await CreateFileAsync(root.Id, "photo.raw", "file content");
            Dictionary<string, string> metadata = new() { ["isClientEncrypted"] = "true", ["description"] = "original" };
            await SetFileMetadataAsync(source.Id, metadata);

            using HttpResponseMessage response = await CopyFileAsync(source.Id, target.Id);
            response.EnsureSuccessStatusCode();
            NodeFileManifestDto copy = (await response.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
            Assert.Multiple(() =>
            {
                Assert.That(copy.Id, Is.Not.EqualTo(source.Id));
                Assert.That(copy.NodeId, Is.EqualTo(target.Id));
                Assert.That(copy.Metadata, Is.EquivalentTo(metadata));
            });
            Assert.That((await GetChildrenAsync(root.Id)).Files.Single().Id, Is.EqualTo(source.Id));
            Assert.That(await _client!.GetStringAsync($"/api/v1/files/{copy.Id}/content"), Is.EqualTo("file content"));
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile storedCopy = await db.NodeFiles.AsNoTracking().SingleAsync(x => x.Id == copy.Id);
            NodeFile storedSource = await db.NodeFiles.AsNoTracking().SingleAsync(x => x.Id == source.Id);
            Assert.Multiple(() =>
            {
                Assert.That(storedCopy.FileManifestId, Is.EqualTo(storedSource.FileManifestId));
                Assert.That(storedCopy.OriginalNodeFileId, Is.EqualTo(storedCopy.Id));
            });
            Assert.That(await db.SyncChanges.AnyAsync(x => x.ItemId == copy.Id && x.Kind == SyncChangeKind.FileCreated), Is.True);
        }

        [Test]
        public async Task CopyFile_ConflictDoesNotModifySourceAndCanRenameOrReplace()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto target = await CreateFolderAsync(root.Id, "target");
            NodeFileManifestDto source = await CreateFileAsync(root.Id, "doc.txt", "source");
            NodeFileManifestDto existing = await CreateFileAsync(target.Id, "doc.txt", "old");
            using HttpResponseMessage conflict = await CopyFileAsync(source.Id, target.Id);
            await AssertConflictKindAsync(conflict, RestoreConflictKind.File);
            using HttpResponseMessage renamed = await CopyFileAsync(source.Id, target.Id, "doc (1).txt");
            renamed.EnsureSuccessStatusCode();
            using HttpResponseMessage replaced = await CopyFileAsync(source.Id, target.Id, overwrite: true);
            replaced.EnsureSuccessStatusCode();
            NodeContentDto children = await GetChildrenAsync(target.Id);
            Assert.That(children.Files.Select(x => x.Name), Is.EquivalentTo(new[] { "doc.txt", "doc (1).txt" }));
            Assert.That(children.Files.All(x => x.Id != existing.Id && x.Id != source.Id), Is.True);
            Assert.That((await GetChildrenAsync(root.Id)).Files.Single().Id, Is.EqualTo(source.Id));
        }

        [Test]
        public async Task CopyFile_ToSameFolder_CannotOverwriteSource()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeFileManifestDto source = await CreateFileAsync(root.Id, "doc.txt", "source");
            using HttpResponseMessage conflict = await CopyFileAsync(source.Id, root.Id, overwrite: true);
            await AssertConflictKindAsync(conflict, RestoreConflictKind.File);
            using HttpResponseMessage renamed = await CopyFileAsync(source.Id, root.Id, "doc (1).txt");
            renamed.EnsureSuccessStatusCode();
            Assert.That((await GetChildrenAsync(root.Id)).Files.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task CopyFolder_CopiesNestedTreeAcrossBatchesAndPreservesMetadata()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto source = await CreateFolderAsync(root.Id, "source");
            NodeDto nested = await CreateFolderAsync(source.Id, "nested");
            NodeDto target = await CreateFolderAsync(root.Id, "target");
            NodeFileManifestDto seed = await CreateFileAsync(nested.Id, "seed.txt", "source");
            using (IServiceScope scope = _factory!.Services.CreateScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                Node folder = await db.Nodes.SingleAsync(x => x.Id == source.Id);
                folder.Metadata = new() { ["policy"] = "preserved" };
                NodeFile seedFile = await db.NodeFiles.SingleAsync(x => x.Id == seed.Id);
                seedFile.Metadata = new() { ["original-name"] = "preserved" };
                Node parent = await db.Nodes.SingleAsync(x => x.Id == nested.Id);
                for (int index = 0; index < 300; index++)
                {
                    db.NodeFiles.Add(seedFile.CopyTo(parent, $"file-{index}.txt"));
                }
                await db.SaveChangesAsync();
                scope.ServiceProvider.GetRequiredService<UserStorageQuotaService>()
                    .RecordLogicalBytesAdded(parent.OwnerId, 1800);
            }
            using HttpResponseMessage quota = await _client!.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-storage-quota-bytes", 3612L);
            quota.EnsureSuccessStatusCode();
            using HttpResponseMessage response = await CopyFolderAsync(source.Id, target.Id);
            response.EnsureSuccessStatusCode();
            NodeDto copy = (await response.Content.ReadFromJsonAsync<NodeDto>())!;
            Assert.That(copy.Metadata!["policy"], Is.EqualTo("preserved"));
            NodeDto child = (await GetChildrenAsync(copy.Id)).Nodes.Single();
            Assert.That(child.Id, Is.Not.EqualTo(nested.Id));
            using HttpResponseMessage childrenResponse = await _client!.GetAsync($"/api/v1/layouts/nodes/{child.Id}/children?pageSize=1000");
            childrenResponse.EnsureSuccessStatusCode();
            NodeContentDto children = (await childrenResponse.Content.ReadFromJsonAsync<NodeContentDto>())!;
            Assert.That(children.Files.Count(), Is.EqualTo(301));
            Assert.That(children.Files.All(x => x.Metadata!["original-name"] == "preserved"), Is.True);
            Assert.That((await GetChildrenAsync(source.Id)).Nodes.Single().Id, Is.EqualTo(nested.Id));
            UserStorageQuotaDto? snapshot = await _client.GetFromJsonAsync<UserStorageQuotaDto>("/api/v1/users/me/storage-quota");
            Assert.That(snapshot!.UsedBytes, Is.EqualTo(3612));
        }

        [Test]
        public async Task CopyFolder_RejectsSelfAndDescendant()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto source = await CreateFolderAsync(root.Id, "source");
            NodeDto nested = await CreateFolderAsync(source.Id, "nested");
            using HttpResponseMessage self = await CopyFolderAsync(source.Id, source.Id);
            using HttpResponseMessage descendant = await CopyFolderAsync(source.Id, nested.Id);
            Assert.Multiple(() =>
            {
                Assert.That(self.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(descendant.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            });
            Assert.That((await GetChildrenAsync(nested.Id)).Nodes, Is.Empty);
        }

        [Test]
        public async Task Copy_RejectsMissingSourcesAndOtherLayouts()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeFileManifestDto file = await CreateFileAsync(root.Id, "doc.txt", "source");
            var (_, otherRoot) = await CreateAdditionalLayoutRootAsync(_factory!.Services, "other");
            using HttpResponseMessage crossLayout = await CopyFileAsync(file.Id, otherRoot);
            using HttpResponseMessage missingFile = await CopyFileAsync(Guid.NewGuid(), root.Id);
            using HttpResponseMessage missingFolder = await CopyFolderAsync(Guid.NewGuid(), root.Id);
            Assert.Multiple(() =>
            {
                Assert.That(crossLayout.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(missingFile.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(missingFolder.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            });
        }

        [Test]
        public async Task CopyFolder_RollsBackAllBatchesWhenQuotaIsExceeded()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto source = await CreateFolderAsync(root.Id, "source");
            NodeDto target = await CreateFolderAsync(root.Id, "target");
            NodeFileManifestDto seed = await CreateFileAsync(source.Id, "seed.txt", "12345");
            using (IServiceScope scope = _factory!.Services.CreateScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                NodeFile file = await db.NodeFiles.SingleAsync(x => x.Id == seed.Id);
                Node parent = await db.Nodes.SingleAsync(x => x.Id == source.Id);
                for (int index = 0; index < 299; index++)
                {
                    db.NodeFiles.Add(file.CopyTo(parent, $"file-{index}.txt"));
                }
                await db.SaveChangesAsync();
                scope.ServiceProvider.GetRequiredService<UserStorageQuotaService>()
                    .RecordLogicalBytesAdded(parent.OwnerId, 1495);
            }
            using HttpResponseMessage quota = await _client!.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-storage-quota-bytes", 2900L);
            quota.EnsureSuccessStatusCode();
            using HttpResponseMessage response = await CopyFolderAsync(source.Id, target.Id);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InsufficientStorage));
            Assert.That((await GetChildrenAsync(target.Id)).Nodes, Is.Empty);
            using IServiceScope verification = _factory!.Services.CreateScope();
            CottonDbContext context = verification.ServiceProvider.GetRequiredService<CottonDbContext>();
            Assert.That(await context.NodeFiles.CountAsync(), Is.EqualTo(300));
            UserStorageQuotaDto? snapshot = await _client.GetFromJsonAsync<UserStorageQuotaDto>("/api/v1/users/me/storage-quota");
            Assert.That(snapshot!.UsedBytes, Is.EqualTo(1500));
        }

        private Task<HttpResponseMessage> CopyFileAsync(Guid id, Guid parentId, string? name = null, bool overwrite = false)
        {
            return _client!.PostAsJsonAsync($"/api/v1/files/{id}/copy", new { parentId, name, overwrite });
        }

        private Task<HttpResponseMessage> CopyFolderAsync(Guid id, Guid parentId)
        {
            return _client!.PostAsJsonAsync($"/api/v1/layouts/nodes/{id}/copy", new { parentId });
        }

        private async Task SetFileMetadataAsync(Guid id, Dictionary<string, string> metadata)
        {
            using IServiceScope scope = _factory!.Services.CreateScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile file = await db.NodeFiles.SingleAsync(x => x.Id == id);
            file.Metadata = metadata;
            await db.SaveChangesAsync();
        }
    }
}
