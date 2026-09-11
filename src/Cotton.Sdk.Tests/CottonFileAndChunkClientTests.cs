// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text;
using Cotton.Auth;
using Cotton.Files;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Tests.Fakes;

namespace Cotton.Sdk.Tests
{
    public class CottonFileAndChunkClientTests
    {
        private const string IfMatchHeaderName = "If-Match";

        [Test]
        public async Task UploadRawAsync_PostsRawBodyToHashEndpoint()
        {
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.Enqueue(HttpStatusCode.Created);
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("chunk"));

            await client.Chunks.UploadRawAsync("abc123", stream);

            Assert.Multiple(() =>
            {
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/chunks/raw?hash=abc123"));
                Assert.That(handler.Requests[0].ContentType, Is.EqualTo("application/octet-stream"));
                Assert.That(Encoding.UTF8.GetString(handler.Requests[0].RawBody), Is.EqualTo("chunk"));
            });
        }

        [Test]
        public async Task UploadRawAsync_RefreshesOnUnauthorizedAndReplaysSeekableStream()
        {
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.Enqueue(HttpStatusCode.Unauthorized, "expired");
            handler.EnqueueJson(HttpStatusCode.OK, new { accessToken = "new-access", refreshToken = "new-refresh" });
            handler.Enqueue(HttpStatusCode.Created);
            InMemoryCottonTokenStore store = new InMemoryCottonTokenStore();
            await store.SaveAsync(new TokenPairDto { AccessToken = "old-access", RefreshToken = "refresh" });
            CottonCloudClient client = new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            });
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("prefixchunk"));
            stream.Position = Encoding.UTF8.GetByteCount("prefix");

            await client.Chunks.UploadRawAsync("abc123", stream);

            TokenPairDto? stored = await store.GetAsync();
            Assert.Multiple(() =>
            {
                Assert.That(stored?.AccessToken, Is.EqualTo("new-access"));
                Assert.That(handler.Requests.Select(x => x.PathAndQuery), Is.EqualTo(new[]
                {
                    "/api/v1/chunks/raw?hash=abc123",
                    "/api/v1/auth/refresh?refreshToken=refresh",
                    "/api/v1/chunks/raw?hash=abc123",
                }));
                Assert.That(handler.Requests[0].AuthorizationParameter, Is.EqualTo("old-access"));
                Assert.That(handler.Requests[2].AuthorizationParameter, Is.EqualTo("new-access"));
                Assert.That(Encoding.UTF8.GetString(handler.Requests[0].RawBody), Is.EqualTo("chunk"));
                Assert.That(Encoding.UTF8.GetString(handler.Requests[2].RawBody), Is.EqualTo("chunk"));
            });
        }

        [Test]
        public async Task CreateFromChunksAsync_MapsRequestAndResponse()
        {
            Guid nodeId = Guid.NewGuid();
            Guid fileId = Guid.NewGuid();
            Guid manifestId = Guid.NewGuid();
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(HttpStatusCode.OK, new
            {
                id = fileId,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow,
                nodeId,
                fileManifestId = manifestId,
                originalNodeFileId = fileId,
                ownerId = Guid.NewGuid(),
                name = "hello.txt",
                contentType = "text/plain",
                sizeBytes = 5,
                contentHash = "hash",
                eTag = "sha256-hash",
                metadata = new Dictionary<string, string> { ["source"] = "test" },
                requiresVideoTranscoding = false,
                previewHashEncryptedHex = (string?)null,
            });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);

            NodeFileManifestDto file = await client.Files.CreateFromChunksAsync(new CreateFileFromChunksRequestDto
            {
                NodeId = nodeId,
                ChunkHashes = ["chunk-hash"],
                Name = "hello.txt",
                ContentType = "text/plain",
                Hash = "hash",
                Validate = true,
            });

            Assert.Multiple(() =>
            {
                Assert.That(file.Id, Is.EqualTo(fileId));
                Assert.That(file.FileManifestId, Is.EqualTo(manifestId));
                Assert.That(file.ContentHash, Is.EqualTo("hash"));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/files/from-chunks"));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"chunkHashes\":[\"chunk-hash\"]"));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"validate\":true"));
            });
        }

        [Test]
        public async Task UpdateContentAsync_SendsExpectedETagAsIfMatch()
        {
            Guid nodeId = Guid.NewGuid();
            Guid fileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(HttpStatusCode.OK, FileManifestPayload(fileId, nodeId, "updated.txt", "sha256-new"));
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);

            await client.Files.UpdateContentAsync(
                fileId,
                new CreateFileFromChunksRequestDto
                {
                    NodeId = nodeId,
                    ChunkHashes = ["chunk-hash"],
                    Name = "updated.txt",
                    ContentType = "text/plain",
                    Hash = "sha256-new",
                    Validate = true,
                },
                expectedETag: "sha256-old");

            Assert.Multiple(() =>
            {
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Patch));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/update-content"));
                Assert.That(handler.Requests[0].Headers[IfMatchHeaderName], Is.EqualTo("\"sha256-old\""));
            });
        }

        [Test]
        public async Task DeleteAsync_SendsExpectedETagAsIfMatch()
        {
            Guid fileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.Enqueue(HttpStatusCode.NoContent);
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);

            await client.Files.DeleteAsync(fileId, skipTrash: true, expectedETag: "\"sha256-current\"");

            Assert.Multiple(() =>
            {
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Delete));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa?skipTrash=true"));
                Assert.That(handler.Requests[0].Headers[IfMatchHeaderName], Is.EqualTo("\"sha256-current\""));
            });
        }

        [Test]
        public async Task MoveAsync_SendsExpectedETagAsIfMatch()
        {
            Guid nodeId = Guid.NewGuid();
            Guid fileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(HttpStatusCode.OK, FileManifestPayload(fileId, nodeId, "moved.txt", "moved-hash"));
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);

            await client.Files.MoveAsync(fileId, nodeId, expectedETag: "sha256-current");

            Assert.Multiple(() =>
            {
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Patch));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/move"));
                Assert.That(handler.Requests[0].Headers[IfMatchHeaderName], Is.EqualTo("\"sha256-current\""));
            });
        }

        [Test]
        public async Task RenameAsync_SendsExpectedETagAsIfMatch()
        {
            Guid nodeId = Guid.NewGuid();
            Guid fileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(HttpStatusCode.OK, FileManifestPayload(fileId, nodeId, "renamed.txt", "renamed-hash"));
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);

            await client.Files.RenameAsync(fileId, " renamed.txt ", expectedETag: "sha256-current");

            Assert.Multiple(() =>
            {
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Patch));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/rename"));
                Assert.That(handler.Requests[0].Headers[IfMatchHeaderName], Is.EqualTo("\"sha256-current\""));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"name\":\"renamed.txt\""));
            });
        }

        [Test]
        public async Task RestoreAsync_MapsRestoreOutcome()
        {
            Guid nodeId = Guid.NewGuid();
            Guid fileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(HttpStatusCode.OK, new
            {
                status = "Restored",
                originalParentPath = "/Archive",
                restoredFile = FileManifestPayload(fileId, nodeId, "restored.txt", "restored-hash"),
            });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);

            RestoreOutcomeDto outcome = await client.Files.RestoreAsync(
                fileId,
                new RestoreItemRequestDto
                {
                    CreateMissingParents = true,
                    Overwrite = true,
                });

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(RestoreStatus.Restored));
                Assert.That(outcome.OriginalParentPath, Is.EqualTo("/Archive"));
                Assert.That(outcome.RestoredFile?.Id, Is.EqualTo(fileId));
                Assert.That(outcome.RestoredFile?.Name, Is.EqualTo("restored.txt"));
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/restore"));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"createMissingParents\":true"));
                Assert.That(handler.Requests[0].Body, Does.Contain("\"overwrite\":true"));
            });
        }

        [Test]
        public async Task GetContentManifestAsync_MapsManifestAndSendsIfMatch()
        {
            Guid fileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            Guid manifestId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            QueuedHttpMessageHandler handler = new QueuedHttpMessageHandler();
            handler.EnqueueJson(HttpStatusCode.OK, new
            {
                nodeFileId = fileId,
                fileManifestId = manifestId,
                contentHash = "full-hash",
                eTag = "sha256-full-hash",
                sizeBytes = 7,
                chunkSizeBytes = 4,
                chunks = new[]
                {
                    new { index = 0, offset = 0, length = 4, hash = "chunk-a", chunkId = "chunk-a" },
                    new { index = 1, offset = 4, length = 3, hash = "chunk-b", chunkId = "chunk-b" },
                },
            });
            CottonCloudClient client = await CreateAuthorizedClientAsync(handler);

            FileContentManifestDto manifest = await client.Files.GetContentManifestAsync(fileId, expectedETag: "sha256-full-hash");

            Assert.Multiple(() =>
            {
                Assert.That(manifest.NodeFileId, Is.EqualTo(fileId));
                Assert.That(manifest.FileManifestId, Is.EqualTo(manifestId));
                Assert.That(manifest.ContentHash, Is.EqualTo("full-hash"));
                Assert.That(manifest.ETag, Is.EqualTo("sha256-full-hash"));
                Assert.That(manifest.SizeBytes, Is.EqualTo(7));
                Assert.That(manifest.ChunkSizeBytes, Is.EqualTo(4));
                Assert.That(manifest.Chunks.Select(x => x.Offset), Is.EqualTo(new long[] { 0, 4 }));
                Assert.That(handler.Requests[0].PathAndQuery, Is.EqualTo("/api/v1/files/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/content-manifest"));
                Assert.That(handler.Requests[0].Headers[IfMatchHeaderName], Is.EqualTo("\"sha256-full-hash\""));
            });
        }

        private static async Task<CottonCloudClient> CreateAuthorizedClientAsync(QueuedHttpMessageHandler handler)
        {
            InMemoryCottonTokenStore store = new InMemoryCottonTokenStore();
            await store.SaveAsync(new TokenPairDto { AccessToken = "access", RefreshToken = "refresh" });
            return new CottonCloudClient(new HttpClient(handler), store, new CottonSdkOptions
            {
                BaseAddress = new Uri("https://cotton.test"),
            });
        }

        private static object FileManifestPayload(Guid fileId, Guid nodeId, string name, string contentHash)
        {
            return new
            {
                id = fileId,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow,
                nodeId,
                fileManifestId = Guid.NewGuid(),
                originalNodeFileId = fileId,
                ownerId = Guid.NewGuid(),
                name,
                contentType = "text/plain",
                sizeBytes = 5,
                contentHash,
                eTag = "sha256-" + contentHash,
                metadata = new Dictionary<string, string>(),
                requiresVideoTranscoding = false,
                previewHashEncryptedHex = (string?)null,
            };
        }
    }
}
