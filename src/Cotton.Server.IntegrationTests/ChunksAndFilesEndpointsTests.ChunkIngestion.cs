// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task Upload_Chunk_And_Create_File_From_It_Works()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // resolve root node
            NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            // upload chunk
            byte[] content = Encoding.UTF8.GetBytes("hello world");
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
            using MultipartFormDataContent form = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                    },
                    "file",
                    "chunk.bin"
                },
                { new StringContent(chunkHashLower), "hash" }
            };
            HttpResponseMessage upRes = await _client.PostAsync("/api/v1/chunks", form);
            upRes.EnsureSuccessStatusCode();

            // create file from chunk
            CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [chunkHashLower],
                Name = "hello.txt",
                ContentType = "text/plain",
                Hash = chunkHashLower,
                NodeId = root!.Id,
                Metadata = new Dictionary<string, string>
                {
                    ["isClientEncrypted"] = "true",
                    ["originalContentType"] = "text/plain"
                }
            };
            HttpResponseMessage createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            createFileRes.EnsureSuccessStatusCode();
            NodeFileManifestDto? created = await createFileRes.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(created.NodeId, Is.EqualTo(root!.Id));
            Assert.That(created.Name, Is.EqualTo("hello.txt"));

            NodeContentDto? list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
            Assert.That(list, Is.Not.Null);
            NodeFileManifestDto? file = list!.Files.SingleOrDefault(x => x.Name == "hello.txt");
            Assert.That(file, Is.Not.Null);
            Assert.That(file!.Id, Is.EqualTo(created.Id));
            Assert.That(file.NodeId, Is.EqualTo(root.Id));
            Assert.That(file!.Metadata, Does.ContainKey("isClientEncrypted"));
            Assert.That(file.Metadata["isClientEncrypted"], Is.EqualTo("true"));
            Assert.That(file.Metadata["originalContentType"], Is.EqualTo("text/plain"));
        }

        [Test]
        public async Task Upload_Raw_Chunk_And_Create_File_From_It_Works()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            byte[] content = Encoding.UTF8.GetBytes("hello raw world");
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
            using ByteArrayContent body = new ByteArrayContent(content)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
            };

            HttpResponseMessage upRes = await _client.PostAsync($"/api/v1/chunks/raw?hash={chunkHashLower}", body);
            upRes.EnsureSuccessStatusCode();

            CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [chunkHashLower],
                Name = "hello-raw.txt",
                ContentType = "text/plain",
                Hash = chunkHashLower,
                NodeId = root!.Id
            };
            HttpResponseMessage createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            createFileRes.EnsureSuccessStatusCode();

            NodeFileManifestDto? created = await createFileRes.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.Name, Is.EqualTo("hello-raw.txt"));
        }

        [Test]
        public async Task Upload_Empty_Raw_Chunk_And_Create_Empty_File_Works()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            byte[] content = [];
            string contentHash = Hasher.ToHexStringHash(Hasher.HashData(content));
            using ByteArrayContent body = new ByteArrayContent(content)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
            };

            HttpResponseMessage uploadResponse = await _client.PostAsync($"/api/v1/chunks/raw?hash={contentHash}", body);
            uploadResponse.EnsureSuccessStatusCode();

            HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [contentHash],
                Name = "empty-raw.txt",
                ContentType = "text/plain",
                Hash = contentHash,
                NodeId = root!.Id,
                Validate = true,
            });
            createResponse.EnsureSuccessStatusCode();

            NodeFileManifestDto? created = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(created, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(created!.Name, Is.EqualTo("empty-raw.txt"));
                Assert.That(created.SizeBytes, Is.Zero);
                Assert.That(created.ContentHash, Is.EqualTo(contentHash));
            });

            byte[] downloaded = await _client.GetByteArrayAsync($"/api/v1/files/{created!.Id}/content");
            Assert.That(downloaded, Is.Empty);
        }

        [Test]
        public async Task Upload_Raw_Chunk_With_Mismatched_Hash_Does_Not_Publish_Storage()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            byte[] content = RandomNumberGenerator.GetBytes(2 * 1024 * 1024);
            byte[] differentContent = content.ToArray();
            differentContent[^1] ^= 0xff;
            string expectedHash = Hasher.ToHexStringHash(Hasher.HashData(differentContent));
            using ByteArrayContent body = new(content)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
            };

            using HttpResponseMessage response = await _client.PostAsync(
                $"/api/v1/chunks/raw?hash={expectedHash}",
                body);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            using IServiceScope scope = _factory!.Services.CreateScope();
            IStoragePipeline storage = scope.ServiceProvider.GetRequiredService<IStoragePipeline>();
            Assert.That(await storage.ExistsAsync(expectedHash), Is.False);
        }

        [Test]
        public async Task Create_File_Returns_Sync_Metadata_In_Create_Response_And_Children_List()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            byte[] content = Encoding.UTF8.GetBytes("sync metadata");
            string contentHash = Hasher.ToHexStringHash(Hasher.HashData(content));
            HttpResponseMessage uploadResponse = await UploadRawChunkAsync(content, contentHash);
            uploadResponse.EnsureSuccessStatusCode();

            HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [contentHash],
                Name = "sync-metadata.txt",
                ContentType = "text/plain",
                Hash = contentHash,
                NodeId = root!.Id,
                Validate = true,
            });
            createResponse.EnsureSuccessStatusCode();

            NodeFileManifestDto? created = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(created, Is.Not.Null);
            AssertSyncMetadata(created!, root.Id, contentHash);
            Assert.That(created!.OriginalNodeFileId, Is.EqualTo(created.Id));

            NodeContentDto? list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root.Id}/children");
            Assert.That(list, Is.Not.Null);
            NodeFileManifestDto? listed = list!.Files.SingleOrDefault(x => x.Id == created.Id);
            Assert.That(listed, Is.Not.Null);
            AssertSyncMetadata(listed!, root.Id, contentHash);
            Assert.That(listed!.FileManifestId, Is.EqualTo(created.FileManifestId));
            Assert.That(listed.OriginalNodeFileId, Is.EqualTo(created.OriginalNodeFileId));
        }

        [Test]
        public async Task Create_File_With_Validation_Can_Reuse_Existing_Uncomputed_Manifest()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            byte[] content = Encoding.UTF8.GetBytes("desktop sync pdf bytes");
            string contentHash = Hasher.ToHexStringHash(Hasher.HashData(content));
            HttpResponseMessage uploadResponse = await UploadRawChunkAsync(content, contentHash);
            uploadResponse.EnsureSuccessStatusCode();

            HttpResponseMessage firstCreateResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [contentHash],
                Name = "existing.pdf",
                ContentType = "application/pdf",
                Hash = contentHash,
                NodeId = root!.Id,
            });
            firstCreateResponse.EnsureSuccessStatusCode();

            HttpResponseMessage secondCreateResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [contentHash],
                Name = "DOG LICENSE.pdf",
                ContentType = "application/pdf",
                Hash = contentHash,
                NodeId = root.Id,
                Validate = true,
            });
            secondCreateResponse.EnsureSuccessStatusCode();
        }

    }
}
