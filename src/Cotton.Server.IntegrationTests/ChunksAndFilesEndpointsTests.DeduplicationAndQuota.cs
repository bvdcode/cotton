// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task Create_And_Update_From_Chunks_Reject_Foreign_Manifest_Hash()
        {
            string ownerToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

            NodeDto? ownerRoot = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(ownerRoot, Is.Not.Null);
            NodeFileManifestDto victimFile = await UploadTextFileAsync(ownerRoot!, "victim-secret.txt", "victim secret bytes");

            HttpResponseMessage createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
            {
                username = "manifestattacker",
                password = "manifestpass",
                role = UserRole.User,
            });
            createUserResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = null;
            string attackerToken = await LoginAsync("manifestattacker", "manifestpass");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", attackerToken);

            NodeDto? attackerRoot = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(attackerRoot, Is.Not.Null);

            HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [],
                Name = "stolen.txt",
                ContentType = "text/plain",
                Hash = victimFile.ContentHash,
                NodeId = attackerRoot!.Id,
            });
            Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            NodeFileManifestDto attackerOwnFile = await UploadTextFileAsync(attackerRoot, "own.txt", "own bytes");
            HttpResponseMessage updateResponse = await _client.PatchAsJsonAsync($"/api/v1/files/{attackerOwnFile.Id}/update-content", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [],
                Name = attackerOwnFile.Name,
                ContentType = "text/plain",
                Hash = victimFile.ContentHash,
                NodeId = attackerRoot.Id,
            });
            Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Upload_Same_Chunk_In_Parallel_Deduplicates_Metadata()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            byte[] content = new byte[2 * 1024 * 1024];
            RandomNumberGenerator.Fill(content);
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));

            HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => UploadRawChunkAsync(content, chunkHashLower)));

            foreach (HttpResponseMessage? response in responses)
            {
                response.EnsureSuccessStatusCode();
                response.Dispose();
            }

            byte[] chunkHash = Hasher.FromHexStringHash(chunkHashLower);
            DbContext.ChangeTracker.Clear();
            Chunk? storedChunk = await DbContext.Chunks
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Hash == chunkHash);
            int ownershipCount = await DbContext.ChunkOwnerships.CountAsync(x => x.ChunkHash == chunkHash);

            Assert.Multiple(() =>
            {
                Assert.That(storedChunk, Is.Not.Null);
                Assert.That(storedChunk?.PlainSizeBytes, Is.EqualTo(content.Length));
                Assert.That(storedChunk?.StoredSizeBytes, Is.GreaterThan(0));
                Assert.That(ownershipCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Create_Files_With_Same_Content_In_Parallel_Reuses_Manifest()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            byte[] content = new byte[128 * 1024];
            RandomNumberGenerator.Fill(content);
            string contentHash = Hasher.ToHexStringHash(Hasher.HashData(content));
            using HttpResponseMessage uploadResponse = await UploadRawChunkAsync(content, contentHash);
            uploadResponse.EnsureSuccessStatusCode();

            const int requestCount = 16;
            Task<HttpResponseMessage>[] requests = Enumerable.Range(0, requestCount)
                .Select(index => _client.PostAsJsonAsync(
                    "/api/v1/files/from-chunks",
                    new CreateFileFromChunksRequestDto
                    {
                        ChunkHashes = [contentHash],
                        Name = $"parallel-manifest-{index}.bin",
                        ContentType = "application/octet-stream",
                        Hash = contentHash,
                        NodeId = root!.Id,
                    }))
                .ToArray();

            HttpResponseMessage[] responses = await Task.WhenAll(requests);
            try
            {
                Assert.That(responses.Select(x => x.StatusCode), Is.All.EqualTo(HttpStatusCode.OK));
            }
            finally
            {
                foreach (HttpResponseMessage response in responses)
                {
                    response.Dispose();
                }
            }

            byte[] proposedHash = Hasher.FromHexStringHash(contentHash);
            DbContext.ChangeTracker.Clear();
            int manifestCount = await DbContext.FileManifests
                .CountAsync(x => x.ProposedContentHash == proposedHash);
            List<Guid> manifestIds = await DbContext.NodeFiles
                .Where(x => x.NodeId == root!.Id && x.Name.StartsWith("parallel-manifest-"))
                .Select(x => x.FileManifestId)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(manifestCount, Is.EqualTo(1));
                Assert.That(manifestIds, Has.Count.EqualTo(requestCount));
                Assert.That(manifestIds.Distinct(), Has.Exactly(1).Items);
            });
        }

        [Test]
        public async Task Chunk_Exists_Honors_CrossUser_Deduplication_Setting()
        {
            string adminToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            byte[] content = Encoding.UTF8.GetBytes("cross-user dedup probe");
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
            using HttpResponseMessage uploadResponse = await UploadRawChunkAsync(content, chunkHashLower);
            uploadResponse.EnsureSuccessStatusCode();

            using HttpResponseMessage disableResponse = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/allow-cross-user-deduplication",
                false);
            disableResponse.EnsureSuccessStatusCode();

            HttpResponseMessage createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
            {
                username = "dedupreader",
                password = "deduppass",
                role = UserRole.User,
            });
            createUserResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = null;
            string otherToken = await LoginAsync("dedupreader", "deduppass");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
            bool visibleWhenDisabled = await _client.GetFromJsonAsync<bool>($"/api/v1/chunks/{chunkHashLower}/exists");
            Assert.That(visibleWhenDisabled, Is.False);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            using HttpResponseMessage enableResponse = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/allow-cross-user-deduplication",
                true);
            enableResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
            bool visibleWhenEnabled = await _client.GetFromJsonAsync<bool>($"/api/v1/chunks/{chunkHashLower}/exists");
            Assert.That(visibleWhenEnabled, Is.True);
        }

        [Test]
        public async Task Create_File_From_Chunks_Reuses_CrossUser_Deduplicated_Chunk()
        {
            string adminToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            byte[] content = Encoding.UTF8.GetBytes("cross-user create from deduplicated chunk");
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
            using HttpResponseMessage uploadResponse = await UploadRawChunkAsync(content, chunkHashLower);
            uploadResponse.EnsureSuccessStatusCode();

            using HttpResponseMessage disableResponse = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/allow-cross-user-deduplication",
                false);
            disableResponse.EnsureSuccessStatusCode();

            using HttpResponseMessage createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
            {
                username = "dedupcreator",
                password = "deduppass",
                role = UserRole.User,
            });
            createUserResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = null;
            string otherToken = await LoginAsync("dedupcreator", "deduppass");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

            NodeDto? otherRoot = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(otherRoot, Is.Not.Null);

            CreateFileFromChunksRequestDto request = new()
            {
                ChunkHashes = [chunkHashLower],
                Name = "cross-dedup.txt",
                ContentType = "text/plain",
                Hash = chunkHashLower,
                NodeId = otherRoot!.Id,
                Validate = true,
            };

            using HttpResponseMessage blockedResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", request);
            Assert.That(blockedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            using HttpResponseMessage enableResponse = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/allow-cross-user-deduplication",
                true);
            enableResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
            bool visibleWhenEnabled = await _client.GetFromJsonAsync<bool>($"/api/v1/chunks/{chunkHashLower}/exists");
            Assert.That(visibleWhenEnabled, Is.True);

            using HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", request);
            createResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto? created = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(created, Is.Not.Null);

            using HttpResponseMessage contentResponse = await _client.GetAsync($"/api/v1/files/{created!.Id}/content");
            contentResponse.EnsureSuccessStatusCode();
            byte[] downloaded = await contentResponse.Content.ReadAsByteArrayAsync();
            Assert.That(downloaded, Is.EqualTo(content));

            DbContext.ChangeTracker.Clear();
            byte[] chunkHash = Hasher.FromHexStringHash(chunkHashLower);
            int ownershipCount = await DbContext.ChunkOwnerships.CountAsync(x => x.ChunkHash == chunkHash);
            Assert.That(ownershipCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Create_And_Update_File_Reject_When_Default_User_Quota_Is_Exceeded()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage quotaResponse = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-storage-quota-bytes",
                5L);
            quotaResponse.EnsureSuccessStatusCode();

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            string fiveByteHash = await UploadChunkAndGetHashAsync("12345");
            HttpResponseMessage createFirstResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [fiveByteHash],
                Name = "five.txt",
                ContentType = "text/plain",
                Hash = fiveByteHash,
                NodeId = root!.Id,
            });
            createFirstResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto? created = await createFirstResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(created, Is.Not.Null);

            string sixByteHash = await UploadChunkAndGetHashAsync("abcdef");
            HttpResponseMessage createSecondResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [sixByteHash],
                Name = "six.txt",
                ContentType = "text/plain",
                Hash = sixByteHash,
                NodeId = root.Id,
            });
            Assert.That(createSecondResponse.StatusCode, Is.EqualTo((HttpStatusCode)507));

            HttpResponseMessage updateResponse = await _client.PatchAsJsonAsync($"/api/v1/files/{created!.Id}/update-content", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [sixByteHash],
                Name = "five.txt",
                ContentType = "text/plain",
                Hash = sixByteHash,
                NodeId = root.Id,
            });
            Assert.That(updateResponse.StatusCode, Is.EqualTo((HttpStatusCode)507));
        }

        [Test]
        public async Task User_Storage_Quota_Snapshot_Tracks_Create_And_Permanent_Delete_From_Cache()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage quotaResponse = await _client.PatchAsJsonAsync(
                "/api/v1/server/settings/default-user-storage-quota-bytes",
                100L);
            quotaResponse.EnsureSuccessStatusCode();

            UserStorageQuotaDto? initialQuota = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.UserStorageQuotaDto>(
                "/api/v1/users/me/storage-quota");
            Assert.That(initialQuota, Is.Not.Null);
            Assert.That(initialQuota!.UsedBytes, Is.EqualTo(0));
            Assert.That(initialQuota.AvailableBytes, Is.EqualTo(100));

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "quota-cache.txt", "12345");
            UserStorageQuotaDto? afterCreate = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.UserStorageQuotaDto>(
                "/api/v1/users/me/storage-quota");
            Assert.That(afterCreate, Is.Not.Null);
            Assert.That(afterCreate!.UsedBytes, Is.EqualTo(5));
            Assert.That(afterCreate.AvailableBytes, Is.EqualTo(95));

            HttpResponseMessage deleteResponse = await _client.DeleteAsync($"/api/v1/files/{file.Id}?skipTrash=true");
            deleteResponse.EnsureSuccessStatusCode();

            UserStorageQuotaDto? afterDelete = await _client.GetFromJsonAsync<Cotton.Server.Models.Dto.UserStorageQuotaDto>(
                "/api/v1/users/me/storage-quota");
            Assert.That(afterDelete, Is.Not.Null);
            Assert.That(afterDelete!.UsedBytes, Is.EqualTo(0));
            Assert.That(afterDelete.AvailableBytes, Is.EqualTo(100));
        }

    }
}
