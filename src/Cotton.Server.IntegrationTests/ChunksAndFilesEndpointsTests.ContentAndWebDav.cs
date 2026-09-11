// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task Download_Owned_File_Content_Works_With_Range_And_ETag()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "owned-content.txt", "0123456789abcdef");

            HttpResponseMessage download = await _client.GetAsync($"/api/v1/files/{file.Id}/content");
            download.EnsureSuccessStatusCode();
            Assert.That(download.Headers.ETag?.Tag, Is.EqualTo($"\"{file.ETag}\""));
            byte[] bytes = await download.Content.ReadAsByteArrayAsync();
            Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo("0123456789abcdef"));

            using HttpRequestMessage rangeRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{file.Id}/content");
            rangeRequest.Headers.Range = new RangeHeaderValue(4, 7);
            HttpResponseMessage range = await _client.SendAsync(rangeRequest);
            Assert.That(range.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
            byte[] rangeBytes = await range.Content.ReadAsByteArrayAsync();
            Assert.Multiple(() =>
            {
                Assert.That(Encoding.UTF8.GetString(rangeBytes), Is.EqualTo("4567"));
                Assert.That(range.Content.Headers.ContentRange?.From, Is.EqualTo(4));
                Assert.That(range.Content.Headers.ContentRange?.To, Is.EqualTo(7));
                Assert.That(range.Content.Headers.ContentRange?.Length, Is.EqualTo(16));
                Assert.That(range.Headers.AcceptRanges, Does.Contain("bytes"));
            });
        }

        [Test]
        public async Task Download_Owned_File_Content_Rejects_Stale_IfMatch()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "stale-range.txt", "0123456789abcdef");

            using HttpRequestMessage rangeRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{file.Id}/content");
            rangeRequest.Headers.Range = new RangeHeaderValue(4, 7);
            rangeRequest.Headers.IfMatch.Add(new EntityTagHeaderValue("\"sha256-stale\""));
            HttpResponseMessage response = await _client.SendAsync(rangeRequest);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
        }

        [Test]
        public async Task Get_Content_Manifest_Returns_Ordered_Chunk_Verification_Metadata()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            byte[] firstChunk = Encoding.UTF8.GetBytes("0123");
            byte[] secondChunk = Encoding.UTF8.GetBytes("456");
            string firstChunkHash = Hasher.ToHexStringHash(Hasher.HashData(firstChunk));
            string secondChunkHash = Hasher.ToHexStringHash(Hasher.HashData(secondChunk));
            (await UploadRawChunkAsync(firstChunk, firstChunkHash)).EnsureSuccessStatusCode();
            (await UploadRawChunkAsync(secondChunk, secondChunkHash)).EnsureSuccessStatusCode();

            byte[] fullContent = [.. firstChunk, .. secondChunk];
            string fullHash = Hasher.ToHexStringHash(Hasher.HashData(fullContent));
            HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [firstChunkHash, secondChunkHash],
                Name = "manifest-range.txt",
                ContentType = "text/plain",
                Hash = fullHash,
                NodeId = root!.Id,
                Validate = true,
            });
            createResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto? created = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(created, Is.Not.Null);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{created!.Id}/content-manifest");
            request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{created.ETag}\""));
            HttpResponseMessage manifestResponse = await _client.SendAsync(request);
            manifestResponse.EnsureSuccessStatusCode();
            FileContentManifestDto? manifest = await manifestResponse.Content.ReadFromJsonAsync<FileContentManifestDto>();

            Assert.That(manifest, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(manifest!.NodeFileId, Is.EqualTo(created.Id));
                Assert.That(manifest.FileManifestId, Is.EqualTo(created.FileManifestId));
                Assert.That(manifest.ContentHash, Is.EqualTo(fullHash));
                Assert.That(manifest.ETag, Is.EqualTo(created.ETag));
                Assert.That(manifest.SizeBytes, Is.EqualTo(7));
                Assert.That(manifest.ChunkSizeBytes, Is.EqualTo(4));
                Assert.That(manifest.Chunks.Select(x => x.Index), Is.EqualTo(new[] { 0, 1 }));
                Assert.That(manifest.Chunks.Select(x => x.Offset), Is.EqualTo(new long[] { 0, 4 }));
                Assert.That(manifest.Chunks.Select(x => x.Length), Is.EqualTo(new long[] { 4, 3 }));
                Assert.That(manifest.Chunks.Select(x => x.Hash), Is.EqualTo(new[] { firstChunkHash, secondChunkHash }));
                Assert.That(manifest.Chunks.Select(x => x.ChunkId), Is.EqualTo(new[] { firstChunkHash, secondChunkHash }));
            });
        }

        [Test]
        public async Task WebDav_File_ETag_Uses_Same_Content_ETag_As_File_Api()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "webdav-etag.txt", "webdav content");
            string quotedETag = $"\"{file.ETag}\"";

            string webDavToken = await GetWebDavTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{webDavToken}")));

            HttpResponseMessage getResponse = await _client.GetAsync("/api/v1/webdav/webdav-etag.txt");
            using HttpRequestMessage headRequest = new HttpRequestMessage(HttpMethod.Head, "/api/v1/webdav/webdav-etag.txt");
            HttpResponseMessage headResponse = await _client.SendAsync(headRequest);
            using HttpRequestMessage propFindRequest = new HttpRequestMessage(new HttpMethod("PROPFIND"), "/api/v1/webdav/webdav-etag.txt");
            propFindRequest.Headers.Add("Depth", "0");
            HttpResponseMessage propFindResponse = await _client.SendAsync(propFindRequest);
            string propFindXml = await propFindResponse.Content.ReadAsStringAsync();

            Assert.Multiple(() =>
            {
                Assert.That(getResponse.Headers.ETag?.Tag, Is.EqualTo(quotedETag));
                Assert.That(headResponse.Headers.ETag?.Tag, Is.EqualTo(quotedETag));
                Assert.That(getResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/plain"));
                Assert.That(headResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/plain"));
                Assert.That(getResponse.Content.Headers.ContentDisposition, Is.Null);
                Assert.That(headResponse.Content.Headers.ContentDisposition, Is.Null);
                Assert.That(getResponse.Headers.GetValues("X-Content-Type-Options"), Does.Contain("nosniff"));
                Assert.That(headResponse.Headers.GetValues("X-Content-Type-Options"), Does.Contain("nosniff"));
                Assert.That(propFindResponse.StatusCode, Is.EqualTo(HttpStatusCode.MultiStatus));
                Assert.That(propFindXml, Does.Contain(quotedETag));
            });
        }

        [Test]
        public async Task WebDav_Dangerous_Content_Is_Forced_To_Attachment()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            const string fileName = "webdav-payload.svg";
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>";
            await UploadTextFileAsync(
                root!,
                fileName,
                svg,
                contentType: "image/svg+xml");

            string webDavToken = await GetWebDavTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{webDavToken}")));

            using HttpResponseMessage getResponse = await _client.GetAsync($"/api/v1/webdav/{fileName}");
            using HttpRequestMessage headRequest = new(HttpMethod.Head, $"/api/v1/webdav/{fileName}");
            using HttpResponseMessage headResponse = await _client.SendAsync(headRequest);

            getResponse.EnsureSuccessStatusCode();
            headResponse.EnsureSuccessStatusCode();
            Assert.That(await getResponse.Content.ReadAsStringAsync(), Is.EqualTo(svg));

            Assert.Multiple(() =>
            {
                AssertWebDavSvgAttachmentHeaders(getResponse, fileName);
                AssertWebDavSvgAttachmentHeaders(headResponse, fileName);
            });
        }

        [Test]
        public async Task WebDav_BasicAuth_Failures_AreRateLimited()
        {
            _ = await LoginAsync();

            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:wrong-webdav-token")));

            for (int i = 0; i < 10; i++)
            {
                HttpResponseMessage failed = await _client.GetAsync("/api/v1/webdav");
                Assert.That(failed.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            }

            HttpResponseMessage limited = await _client.GetAsync("/api/v1/webdav");
            Assert.That(limited.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        }

        [Test]
        public async Task WebDav_BasicAuth_RejectsMultilinePayload()
        {
            string accessToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            string webDavToken = await GetWebDavTokenAsync();
            string payload = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"ignored\ntestuser:{webDavToken}"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", payload);

            using HttpResponseMessage response = await _client.GetAsync("/api/v1/webdav");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Download_Owned_File_Content_Rejects_Another_User()
        {
            string ownerToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeFileManifestDto file = await UploadTextFileAsync(root!, "private-content.txt", "private");

            HttpResponseMessage createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
            {
                username = "synccontentuser",
                password = "synccontentpass",
                role = UserRole.User,
            });
            createUserResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = null;
            string otherToken = await LoginAsync("synccontentuser", "synccontentpass");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

            HttpResponseMessage response = await _client.GetAsync($"/api/v1/files/{file.Id}/content");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

    }
}
