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
        public async Task Download_Owned_File_Content_By_Chunk_Reassembles_Multiple_Chunks()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            string firstHash = await UploadChunkAndGetHashAsync("abc");
            string secondHash = await UploadChunkAndGetHashAsync("defgh");
            string fullHash = Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes("abcdefgh")));
            using HttpResponseMessage createResponse = await _client.PostAsJsonAsync(
                "/api/v1/files/from-chunks",
                new CreateFileFromChunksRequestDto
                {
                    ChunkHashes = [firstHash, secondHash],
                    Name = "chunked.txt",
                    ContentType = "text/plain",
                    Hash = fullHash,
                    NodeId = root!.Id,
                });
            createResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto? file = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(file, Is.Not.Null);

            using HttpRequestMessage firstRequest = new(HttpMethod.Get, $"/api/v1/files/{file!.Id}/content?chunkNumber=0");
            firstRequest.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{file.ETag}\""));
            using HttpResponseMessage first = await _client.SendAsync(firstRequest);
            using HttpResponseMessage second = await _client.GetAsync($"/api/v1/files/{file.Id}/content?chunkNumber=1");

            Assert.Multiple(() =>
            {
                Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(first.Headers.GetValues("X-Cotton-Chunk-Count"), Does.Contain("2"));
                Assert.That(second.Headers.GetValues("X-Cotton-Chunk-Count"), Does.Contain("2"));
                Assert.That(first.Headers.ETag?.Tag, Is.EqualTo($"\"{file.ETag}\""));
                Assert.That(second.Headers.ETag?.Tag, Is.EqualTo($"\"{file.ETag}\""));
                Assert.That(first.Content.Headers.ContentLength, Is.EqualTo(3));
                Assert.That(second.Content.Headers.ContentLength, Is.EqualTo(5));
                Assert.That(first.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/octet-stream"));
            });
            byte[] firstBytes = await first.Content.ReadAsByteArrayAsync();
            byte[] secondBytes = await second.Content.ReadAsByteArrayAsync();
            Assert.That(Encoding.UTF8.GetString([.. firstBytes, .. secondBytes]), Is.EqualTo("abcdefgh"));

            using HttpResponseMessage outOfRange = await _client.GetAsync($"/api/v1/files/{file.Id}/content?chunkNumber=2");
            using HttpResponseMessage negative = await _client.GetAsync($"/api/v1/files/{file.Id}/content?chunkNumber=-1");
            using HttpRequestMessage staleRequest = new(HttpMethod.Get, $"/api/v1/files/{file.Id}/content?chunkNumber=1");
            staleRequest.Headers.IfMatch.Add(new EntityTagHeaderValue("\"sha256-stale\""));
            using HttpResponseMessage stale = await _client.SendAsync(staleRequest);
            using HttpRequestMessage rangeRequest = new(HttpMethod.Get, $"/api/v1/files/{file.Id}/content?chunkNumber=1");
            rangeRequest.Headers.Range = new RangeHeaderValue(0, 1);
            using HttpResponseMessage range = await _client.SendAsync(rangeRequest);
            Assert.Multiple(() =>
            {
                Assert.That(outOfRange.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(negative.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(stale.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
                Assert.That(range.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            });
        }

        [Test]
        public async Task WebDav_UsesNodeFileContentType_AndSameContentETagAsFileApi()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto text = await UploadTextFileAsync(root!, "webdav-etag.txt", "webdav content");
            NodeFileManifestDto file = await UploadTextFileAsync(root!, "webdav-etag.md", "webdav content");
            Assert.That(file.FileManifestId, Is.EqualTo(text.FileManifestId));
            string quotedETag = $"\"{file.ETag}\"";

            string webDavToken = await GetWebDavTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{webDavToken}")));

            HttpResponseMessage getResponse = await _client.GetAsync("/api/v1/webdav/webdav-etag.md");
            using HttpRequestMessage headRequest = new HttpRequestMessage(HttpMethod.Head, "/api/v1/webdav/webdav-etag.md");
            HttpResponseMessage headResponse = await _client.SendAsync(headRequest);
            using HttpRequestMessage propFindRequest = new HttpRequestMessage(new HttpMethod("PROPFIND"), "/api/v1/webdav/webdav-etag.md");
            propFindRequest.Headers.Add("Depth", "0");
            HttpResponseMessage propFindResponse = await _client.SendAsync(propFindRequest);
            string propFindXml = await propFindResponse.Content.ReadAsStringAsync();
            using HttpRequestMessage listRequest = new(new HttpMethod("PROPFIND"), "/api/v1/webdav/");
            listRequest.Headers.Add("Depth", "1");
            using HttpResponseMessage listResponse = await _client.SendAsync(listRequest);
            string listXml = await listResponse.Content.ReadAsStringAsync();

            Assert.Multiple(() =>
            {
                Assert.That(getResponse.Headers.ETag?.Tag, Is.EqualTo(quotedETag));
                Assert.That(headResponse.Headers.ETag?.Tag, Is.EqualTo(quotedETag));
                Assert.That(getResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/markdown"));
                Assert.That(headResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/markdown"));
                Assert.That(getResponse.Content.Headers.ContentDisposition, Is.Null);
                Assert.That(headResponse.Content.Headers.ContentDisposition, Is.Null);
                Assert.That(getResponse.Headers.GetValues("X-Content-Type-Options"), Does.Contain("nosniff"));
                Assert.That(headResponse.Headers.GetValues("X-Content-Type-Options"), Does.Contain("nosniff"));
                Assert.That(propFindResponse.StatusCode, Is.EqualTo(HttpStatusCode.MultiStatus));
                Assert.That(propFindXml, Does.Contain(quotedETag));
                Assert.That(propFindXml, Does.Contain("<d:getcontenttype>text/markdown</d:getcontenttype>"));
                Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.MultiStatus));
                Assert.That(listXml, Does.Contain("<d:getcontenttype>text/markdown</d:getcontenttype>"));
                Assert.That(listXml, Does.Contain("<d:getcontenttype>text/plain</d:getcontenttype>"));
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
