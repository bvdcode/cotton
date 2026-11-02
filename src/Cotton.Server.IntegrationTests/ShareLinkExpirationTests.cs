// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class ShareLinkExpirationTests : FileEndpointTestBase
    {
        [TestCase(null, 1440)]
        [TestCase("", null)]
        [TestCase("1576800", 1576800)]
        [TestCase("5256000", 5256000)]
        public async Task FileAndFolderLinks_PreserveRequestedExpiration_AndCanBeRevoked(
            string? queryValue, int? expectedMinutes)
        {
            string authToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeDto folder = await CreateFolderAsync(root.Id, "Shared folder");
            NodeFileManifestDto file = await UploadTextFileAsync(folder, "shared.txt", "shared content");
            string query = queryValue is null ? "" : $"?expireAfterMinutes={queryValue}";
            DateTime before = DateTime.UtcNow;

            using HttpResponseMessage fileLinkResponse = await _client.GetAsync($"/api/v1/files/{file.Id}/download-link{query}");
            fileLinkResponse.EnsureSuccessStatusCode();
            string fileLink = (await fileLinkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            using HttpResponseMessage folderLinkResponse = await _client.GetAsync($"/api/v1/layouts/nodes/{folder.Id}/share-link{query}");
            folderLinkResponse.EnsureSuccessStatusCode();

            DownloadToken fileToken = await DbContext.DownloadTokens.AsNoTracking().SingleAsync(x => x.NodeFileId == file.Id);
            NodeShareToken folderToken = await DbContext.NodeShareTokens.AsNoTracking().SingleAsync(x => x.NodeId == folder.Id);
            foreach (DateTime? expiration in new[] { fileToken.ExpiresAt, folderToken.ExpiresAt })
            {
                if (expectedMinutes.HasValue)
                {
                    Assert.That(expiration, Is.InRange(before.AddMinutes(expectedMinutes.Value), DateTime.UtcNow.AddMinutes(expectedMinutes.Value)));
                }
                else
                {
                    Assert.That(expiration, Is.Null);
                }
            }

            _client.DefaultRequestHeaders.Authorization = null;
            using HttpResponseMessage download = await _client.GetAsync(fileLink);
            download.EnsureSuccessStatusCode();
            Assert.That(await download.Content.ReadAsStringAsync(), Is.EqualTo("shared content"));
            using HttpResponseMessage listing = await _client.GetAsync($"/api/v1/layouts/shared/{folderToken.Token}/children");
            listing.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            using HttpResponseMessage revoke = await _client.PostAsync("/api/v1/auth/invalidate-share-links", null);
            revoke.EnsureSuccessStatusCode();
            _client.DefaultRequestHeaders.Authorization = null;
            using HttpResponseMessage revokedFile = await _client.GetAsync(fileLink);
            using HttpResponseMessage revokedFolder = await _client.GetAsync($"/api/v1/layouts/shared/{folderToken.Token}/children");
            Assert.Multiple(() =>
            {
                Assert.That(revokedFile.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(revokedFolder.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            });
        }
    }
}
