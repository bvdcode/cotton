// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using System.Text.Json;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [TestCase("inline", "inline")]
        [TestCase("download", "attachment")]
        public async Task Share_Head_PreservesFileHeadersAndOneTimeToken(string view, string disposition)
        {
            string accessToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            const string content = "shared-file-response";
            NodeFileManifestDto file = await UploadTextFileAsync(root, "headers.txt", content);
            using HttpResponseMessage linkResponse = await _client.GetAsync(
                $"/api/v1/files/{file.Id}/download-link?deleteAfterUse=true");
            linkResponse.EnsureSuccessStatusCode();
            string token = ExtractToken((await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"'));
            _client.DefaultRequestHeaders.Authorization = null;

            using HttpRequestMessage request = new(HttpMethod.Head, $"/s/{token}?view={view}");
            using HttpResponseMessage head = await _client.SendAsync(request);
            head.EnsureSuccessStatusCode();
            Assert.Multiple(() =>
            {
                Assert.That(head.Content.Headers.ContentLength, Is.EqualTo(content.Length));
                Assert.That(head.Content.Headers.ContentDisposition?.DispositionType, Is.EqualTo(disposition));
                Assert.That(head.Content.Headers.ContentDisposition?.FileNameStar, Is.EqualTo("headers.txt"));
                Assert.That(head.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/plain"));
                Assert.That(head.Content.Headers.ContentEncoding, Does.Contain("identity"));
                Assert.That(head.Headers.ETag, Is.Not.Null);
                Assert.That(head.Headers.CacheControl?.NoStore, Is.True);
            });
            Assert.That(await head.Content.ReadAsByteArrayAsync(), Is.Empty);
            Assert.That(await DbContext.DownloadTokens.AnyAsync(x => x.Token == token), Is.True);

            using HttpResponseMessage download = await _client.GetAsync($"/s/{token}?view=download");
            download.EnsureSuccessStatusCode();
            Assert.That(await download.Content.ReadAsStringAsync(), Is.EqualTo(content));
            Assert.That(download.Headers.ETag, Is.EqualTo(head.Headers.ETag));
            Assert.That(await WaitForDownloadTokenAsync(token, expectedExists: false), Is.False);
        }

        [TestCase("invalid", 400, "bad_request", "Invalid view mode. Valid values: page, download, inline.")]
        [TestCase("download", 404, "not_found", "File not found")]
        public async Task Share_Error_PreservesProblemDetailsContract(string view, int status, string code, string detail)
        {
            const string path = "/s/missing-share-response-token";
            using HttpResponseMessage response = await _client!.GetAsync($"{path}?view={view}");
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement body = json.RootElement;
            Assert.Multiple(() =>
            {
                Assert.That((int)response.StatusCode, Is.EqualTo(status));
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
                Assert.That(body.GetProperty("status").GetInt32(), Is.EqualTo(status));
                Assert.That(body.GetProperty("detail").GetString(), Is.EqualTo(detail));
                Assert.That(body.GetProperty("code").GetString(), Is.EqualTo(code));
                Assert.That(body.GetProperty("instance").GetString(), Is.EqualTo(path));
                Assert.That(body.GetProperty("traceId").GetString(), Is.Not.Null.And.Not.Empty);
            });
        }
    }
}
