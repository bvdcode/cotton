// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task Share_RangeMetadataProbe_DoesNotConsume_DeleteAfterUse_Token()
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "range-probe.txt", "0123456789abcdef");
            HttpResponseMessage linkResponse = await _client.GetAsync($"/api/v1/files/{file.Id}/download-link?deleteAfterUse=true");
            linkResponse.EnsureSuccessStatusCode();
            string downloadLink = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            string shareToken = ExtractToken(downloadLink);

            _client.DefaultRequestHeaders.Authorization = null;
            using HttpRequestMessage probeRequest = new HttpRequestMessage(HttpMethod.Get, $"/s/{shareToken}?view=inline");
            probeRequest.Headers.Range = new RangeHeaderValue(0, 3);
            HttpResponseMessage probeResponse = await _client.SendAsync(probeRequest);
            Assert.That(probeResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.PartialContent));
            _ = await probeResponse.Content.ReadAsByteArrayAsync();

            DbContext.ChangeTracker.Clear();
            bool existsAfterProbe = await DbContext.DownloadTokens.AnyAsync(x => x.Token == shareToken);
            Assert.That(existsAfterProbe, Is.True);

            HttpResponseMessage downloadResponse = await _client.GetAsync($"/s/{shareToken}?view=download");
            downloadResponse.EnsureSuccessStatusCode();
            _ = await downloadResponse.Content.ReadAsByteArrayAsync();

            bool existsAfterDownload = await WaitForDownloadTokenAsync(shareToken, expectedExists: false);
            Assert.That(existsAfterDownload, Is.False);
        }

        [Test]
        public async Task Share_InlinePreview_ServesSmallPreview_WithoutConsuming_DeleteAfterUse_Token()
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(root!, "shared-apk.apk", "apk payload");
            byte[] previewBytes = CreateWebpSignatureBytes("shared preview");
            byte[] previewHash = Hasher.HashData(previewBytes);
            await StoreSmallPreviewAsync(file.Id, previewHash, previewBytes);

            HttpResponseMessage linkResponse = await _client.GetAsync($"/api/v1/files/{file.Id}/download-link?deleteAfterUse=true");
            linkResponse.EnsureSuccessStatusCode();
            string downloadLink = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            string shareToken = ExtractToken(downloadLink);

            _client.DefaultRequestHeaders.Authorization = null;
            using HttpRequestMessage previewHeadRequest = new HttpRequestMessage(HttpMethod.Head, $"/s/{shareToken}?view=inline&preview=true");
            HttpResponseMessage previewHeadResponse = await _client.SendAsync(previewHeadRequest);
            previewHeadResponse.EnsureSuccessStatusCode();

            Assert.Multiple(() =>
            {
                Assert.That(previewHeadResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
                Assert.That(previewHeadResponse.Content.Headers.ContentDisposition?.DispositionType, Is.Not.EqualTo("attachment"));
            });

            DbContext.ChangeTracker.Clear();
            bool existsAfterPreviewHead = await DbContext.DownloadTokens.AnyAsync(x => x.Token == shareToken);
            Assert.That(existsAfterPreviewHead, Is.True);

            HttpResponseMessage previewResponse = await _client.GetAsync($"/s/{shareToken}?view=inline&preview=true");
            previewResponse.EnsureSuccessStatusCode();
            byte[] servedPreview = await previewResponse.Content.ReadAsByteArrayAsync();

            Assert.Multiple(() =>
            {
                Assert.That(previewResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/webp"));
                Assert.That(previewResponse.Content.Headers.ContentDisposition?.DispositionType, Is.Not.EqualTo("attachment"));
                Assert.That(servedPreview, Is.EqualTo(previewBytes));
            });

            DbContext.ChangeTracker.Clear();
            bool existsAfterPreview = await DbContext.DownloadTokens.AnyAsync(x => x.Token == shareToken);
            Assert.That(existsAfterPreview, Is.True);

            HttpResponseMessage downloadResponse = await _client.GetAsync($"/s/{shareToken}?view=download");
            downloadResponse.EnsureSuccessStatusCode();
            _ = await downloadResponse.Content.ReadAsByteArrayAsync();

            bool existsAfterDownload = await WaitForDownloadTokenAsync(shareToken, expectedExists: false);
            Assert.That(existsAfterDownload, Is.False);
        }

        [Test]
        public async Task Share_Inline_Dangerous_Svg_Is_Forced_To_Attachment()
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>fetch('/api/v1/auth/refresh',{method:'POST'})</script></svg>";
            byte[] content = Encoding.UTF8.GetBytes(svg);
            string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
            HttpResponseMessage uploadResponse = await UploadRawChunkAsync(content, hash);
            uploadResponse.EnsureSuccessStatusCode();

            HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [hash],
                Name = "payload.svg",
                ContentType = "image/svg+xml",
                Hash = hash,
                NodeId = root!.Id,
            });
            createResponse.EnsureSuccessStatusCode();
            NodeFileManifestDto? file = await createResponse.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(file, Is.Not.Null);

            HttpResponseMessage linkResponse = await _client.GetAsync($"/api/v1/files/{file!.Id}/download-link");
            linkResponse.EnsureSuccessStatusCode();
            string downloadLink = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            string shareToken = ExtractToken(downloadLink);

            _client.DefaultRequestHeaders.Authorization = null;
            HttpResponseMessage inlineResponse = await _client.GetAsync($"/s/{shareToken}?view=inline");
            inlineResponse.EnsureSuccessStatusCode();

            Assert.Multiple(() =>
            {
                Assert.That(inlineResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/octet-stream"));
                Assert.That(inlineResponse.Content.Headers.ContentDisposition?.DispositionType, Is.EqualTo("attachment"));
                Assert.That(inlineResponse.Headers.TryGetValues("X-Content-Type-Options", out IEnumerable<string>? values), Is.True);
                Assert.That(values, Does.Contain("nosniff"));
            });
        }

        [Test]
        public async Task File_Custom_Share_Token_Cannot_Collide_With_Folder_Share_Token()
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto folder = await CreateFolderAsync(root!.Id, "shared-folder");
            NodeFileManifestDto file = await UploadTextFileAsync(root, "shared-file.txt", "file body");

            const string token = "shared-token-collision";
            HttpResponseMessage folderShare = await _client.GetAsync($"/api/v1/layouts/nodes/{folder.Id}/share-link?customToken={token}");
            folderShare.EnsureSuccessStatusCode();

            HttpResponseMessage fileShare = await _client.GetAsync($"/api/v1/files/{file.Id}/download-link?customToken={token}");
            Assert.That(fileShare.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public async Task Folder_Custom_Share_Token_Cannot_Collide_With_File_Share_Token()
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeFileManifestDto file = await UploadTextFileAsync(root!, "shared-file.txt", "file body");
            NodeDto folder = await CreateFolderAsync(root!.Id, "shared-folder");

            const string token = "shared-token-collision";
            HttpResponseMessage fileShare = await _client.GetAsync(
                $"/api/v1/files/{file.Id}/download-link?customToken={token}");
            fileShare.EnsureSuccessStatusCode();

            HttpResponseMessage folderShare = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/share-link?customToken={token}");
            Assert.That(folderShare.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public async Task Generated_File_And_Folder_Share_Tokens_Are_Eight_Lowercase_Alphanumeric_Characters()
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeDto folder = await CreateFolderAsync(root!.Id, "shared-folder-token-format");
            NodeFileManifestDto file = await UploadTextFileAsync(root, "shared-file-token-format.txt", "file body");

            HttpResponseMessage folderResponse = await _client.GetAsync($"/api/v1/layouts/nodes/{folder.Id}/share-link");
            folderResponse.EnsureSuccessStatusCode();
            string folderLink = (await folderResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            string folderToken = folderLink.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

            HttpResponseMessage fileResponse = await _client.GetAsync($"/api/v1/files/{file.Id}/download-link");
            fileResponse.EnsureSuccessStatusCode();
            string fileLink = (await fileResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            string fileToken = ExtractToken(fileLink);

            Assert.Multiple(() =>
            {
                Assert.That(folderToken, Does.Match("^[a-z0-9]{8}$"));
                Assert.That(fileToken, Does.Match("^[a-z0-9]{8}$"));
            });
        }

        [Test]
        public async Task Public_Share_Failed_Compact_Lookups_Block_Compact_Tokens_Before_Resolution()
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeFileManifestDto file = await UploadTextFileAsync(root!, "rate-limit-content.txt", "valid shared content");
            NodeDto folder = await CreateFolderAsync(root!.Id, "rate-limit-folder");
            NodeDto expandedTokenFolder = await CreateFolderAsync(root!.Id, "expanded-rate-limit-folder");

            HttpResponseMessage linkResponse = await _client.GetAsync($"/api/v1/files/{file.Id}/download-link");
            linkResponse.EnsureSuccessStatusCode();
            string downloadLink = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            string shareToken = ExtractToken(downloadLink);
            HttpResponseMessage folderLinkResponse = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{folder.Id}/share-link");
            folderLinkResponse.EnsureSuccessStatusCode();
            string folderLink = (await folderLinkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            string folderToken = folderLink.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
            const string expandedFolderToken = "LongTokenAb1";
            HttpResponseMessage expandedFolderLinkResponse = await _client.GetAsync(
                $"/api/v1/layouts/nodes/{expandedTokenFolder.Id}/share-link?customToken={expandedFolderToken}");
            expandedFolderLinkResponse.EnsureSuccessStatusCode();
            _client.DefaultRequestHeaders.Authorization = null;

            for (int i = 0; i < 60; i++)
            {
                string missingToken = $"s{i:D7}";
                using HttpResponseMessage response = await _client.GetAsync($"/s/{missingToken}");
                Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.TooManyRequests));
            }

            using HttpRequestMessage contentRequest = new(HttpMethod.Get, $"/s/{shareToken}?view=inline");
            contentRequest.Headers.Range = new RangeHeaderValue(0, 3);
            using HttpResponseMessage blockedContentResponse = await _client.SendAsync(contentRequest);
            using HttpResponseMessage blockedFolderResponse = await _client.GetAsync(
                $"/api/v1/layouts/shared/{folderToken}");
            using HttpResponseMessage expandedTokenResponse = await _client.GetAsync(
                $"/api/v1/layouts/shared/{expandedFolderToken}");
            Assert.Multiple(() =>
            {
                Assert.That(blockedContentResponse.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                Assert.That(blockedContentResponse.Headers.RetryAfter, Is.Not.Null);
                Assert.That(blockedFolderResponse.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                Assert.That(expandedTokenResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            });
        }

        [Test]
        public async Task Direct_And_Hls_Failed_Compact_Lookups_Block_Before_Resolution()
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeFileManifestDto file = await UploadTextFileAsync(
                root!,
                "direct-rate-limit-content.txt",
                "valid direct content");

            using HttpResponseMessage linkResponse = await _client.GetAsync(
                $"/api/v1/files/{file.Id}/download-link");
            linkResponse.EnsureSuccessStatusCode();
            string downloadLink = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            string shareToken = ExtractToken(downloadLink);
            const string expandedToken = "LongTokenAb1";
            using HttpResponseMessage expandedLinkResponse = await _client.GetAsync(
                $"/api/v1/files/{file.Id}/download-link?customToken={expandedToken}");
            expandedLinkResponse.EnsureSuccessStatusCode();
            string expandedDownloadLink = (await expandedLinkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            _client.DefaultRequestHeaders.Authorization = null;

            for (int i = 0; i < 60; i++)
            {
                string missingToken = $"d{i:D7}";
                using HttpResponseMessage response = await _client.GetAsync(
                    $"/api/v1/files/{file.Id}/download?token={missingToken}");
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            }

            using HttpResponseMessage blockedDownloadResponse = await _client.GetAsync(downloadLink);
            using HttpResponseMessage blockedHlsResponse = await _client.GetAsync(
                $"/api/v1/files/{file.Id}/hls/master.m3u8?token={shareToken}");
            using HttpResponseMessage expandedDownloadResponse = await _client.GetAsync(expandedDownloadLink);
            using HttpResponseMessage expandedHlsResponse = await _client.GetAsync(
                $"/api/v1/files/{file.Id}/hls/master.m3u8?token={expandedToken}");

            Assert.Multiple(() =>
            {
                Assert.That(blockedDownloadResponse.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                Assert.That(blockedDownloadResponse.Headers.RetryAfter, Is.Not.Null);
                Assert.That(blockedHlsResponse.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                Assert.That(expandedDownloadResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(expandedHlsResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            });
        }

    }
}
