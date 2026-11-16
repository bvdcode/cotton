// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class FileWebDavContentTypeMigrationTests : FileEndpointTestBase
    {
        [Test]
        public async Task WebDav_LegacyFile_WorksBeforeAndAfterRename()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            const string content = "0123456789abcdef";
            NodeFileManifestDto created = await UploadTextFileAsync(root, "legacy-webdav.txt", content);
            string webDavToken = await GetWebDavTokenAsync();

            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile file = await dbContext.NodeFiles.SingleAsync(entity => entity.Id == created.Id);
            dbContext.Entry(file).Property(entity => entity.ContentType).CurrentValue = string.Empty;
            await dbContext.SaveChangesAsync();
            await DatabaseIntegrityTestSignatures.SetVersionAsync(dbContext, file, 1, scope.ServiceProvider);
            dbContext.ChangeTracker.Clear();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"testuser:{webDavToken}")));

            foreach (bool renamed in new[] { false, true })
            {
                string name = "legacy-webdav.txt";
                if (renamed)
                {
                    name = "renamed-webdav.txt";
                    file = await dbContext.NodeFiles.SingleAsync(entity => entity.Id == created.Id);
                    file.SetName(name);
                    await dbContext.SaveChangesAsync();
                }
                string expectedContentType = renamed ? "text/plain" : "application/octet-stream";
                using HttpResponseMessage getResponse = await _client.GetAsync($"/api/v1/webdav/{name}");
                using HttpRequestMessage headRequest = new(HttpMethod.Head, $"/api/v1/webdav/{name}");
                using HttpResponseMessage headResponse = await _client.SendAsync(headRequest);
                using HttpRequestMessage rangeRequest = new(HttpMethod.Get, $"/api/v1/webdav/{name}");
                rangeRequest.Headers.Range = new RangeHeaderValue(4, 7);
                using HttpResponseMessage rangeResponse = await _client.SendAsync(rangeRequest);

                Assert.Multiple(() =>
                {
                    Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(headResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(rangeResponse.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
                    Assert.That(getResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo(expectedContentType));
                    Assert.That(headResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo(expectedContentType));
                    Assert.That(rangeResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo(expectedContentType));
                    Assert.That(getResponse.Content.Headers.ContentLength, Is.EqualTo(content.Length));
                    Assert.That(headResponse.Content.Headers.ContentLength, Is.EqualTo(content.Length));
                    Assert.That(getResponse.Headers.ETag?.Tag, Is.EqualTo($"\"{created.ETag}\""));
                    Assert.That(headResponse.Headers.ETag, Is.EqualTo(getResponse.Headers.ETag));
                    Assert.That(rangeResponse.Headers.ETag, Is.EqualTo(getResponse.Headers.ETag));
                    Assert.That(rangeResponse.Content.Headers.ContentRange?.From, Is.EqualTo(4));
                    Assert.That(rangeResponse.Content.Headers.ContentRange?.To, Is.EqualTo(7));
                    Assert.That(getResponse.Headers.GetValues("X-Content-Type-Options"), Does.Contain("nosniff"));
                    Assert.That(headResponse.Headers.GetValues("X-Content-Type-Options"), Does.Contain("nosniff"));
                });
                Assert.That(await getResponse.Content.ReadAsStringAsync(), Is.EqualTo(content));
                Assert.That(await headResponse.Content.ReadAsByteArrayAsync(), Is.Empty);
                Assert.That(await rangeResponse.Content.ReadAsStringAsync(), Is.EqualTo("4567"));
                NodeFile stored = await dbContext.NodeFiles.SingleAsync(entity => entity.Id == created.Id);
                Assert.That(stored.ContentType, Is.EqualTo(renamed ? "text/plain" : string.Empty));
                Assert.That(dbContext.Entry(stored).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                    Is.EqualTo(renamed ? NodeFileIntegrityDescriptor.LatestVersion : 1));
                scope.ServiceProvider.GetRequiredService<IDatabaseIntegrityVerifier>()
                    .RequireValid(dbContext, stored, "test.webdav-content-type");
                dbContext.ChangeTracker.Clear();
            }
        }
    }
}
