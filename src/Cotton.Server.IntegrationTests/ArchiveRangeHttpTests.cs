// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Requests;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class ArchiveRangeHttpTests : FileEndpointTestBase
    {
        [Test]
        public async Task ParallelDownloads_WithColdChecksums_ReconstructArchive()
        {
            ArchiveDownloadLinkDto link = await CreateArchiveAsync();
            using HttpRequestMessage headRequest = new(HttpMethod.Head, link.Url);
            using HttpResponseMessage head = await _client!.SendAsync(headRequest);
            head.EnsureSuccessStatusCode();
            long size = head.Content.Headers.ContentLength!.Value;
            const int connections = 8;
            long partSize = (size + connections - 1) / connections;
            Task<byte[]>[] downloads = Enumerable.Range(0, connections).Select(async index =>
            {
                long start = index * partSize;
                long end = Math.Min(size - 1, start + partSize - 1);
                using HttpResponseMessage part = await GetRangeAsync(link.Url, $"bytes={start}-{end}", head.Headers.ETag!.ToString());
                Assert.Multiple(() =>
                {
                    Assert.That(part.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
                    Assert.That(part.Content.Headers.ContentRange?.From, Is.EqualTo(start));
                    Assert.That(part.Content.Headers.ContentRange?.To, Is.EqualTo(end));
                    Assert.That(part.Headers.ETag, Is.EqualTo(head.Headers.ETag));
                });
                return await part.Content.ReadAsByteArrayAsync();
            }).ToArray();
            byte[][] parts = await Task.WhenAll(downloads);
            byte[] combined = parts.SelectMany(part => part).ToArray();
            using HttpResponseMessage full = await _client.GetAsync(link.Url);
            Assert.That(combined, Is.EqualTo(await full.Content.ReadAsByteArrayAsync()));
            using ZipArchive zip = new(new MemoryStream(combined));
            foreach (ZipArchiveEntry entry in zip.Entries.Where(entry => !entry.FullName.EndsWith('/')))
            {
                using StreamReader reader = new(await entry.OpenAsync());
                string expected = entry.Name == "первый.txt" ? new string('a', 4096) : new string('b', 8192);
                Assert.That(await reader.ReadToEndAsync(), Is.EqualTo(expected));
            }
        }

        [Test]
        public async Task ResumeAcrossStorageChunks_ReturnsOriginalBytes()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            string first = new('a', 131_073);
            string second = new('b', 65_537);
            string firstHash = await UploadChunkAndGetHashAsync(first);
            string secondHash = await UploadChunkAndGetHashAsync(second);
            CreateFileFromChunksRequestDto file = new()
            {
                NodeId = root.Id,
                Name = "chunks.bin",
                ContentType = "application/octet-stream",
                ChunkHashes = [firstHash, secondHash],
                Hash = Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(first + second))),
            };
            using HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", file);
            created.EnsureSuccessStatusCode();
            NodeFileManifestDto stored = (await created.Content.ReadFromJsonAsync<NodeFileManifestDto>())!;
            using HttpResponseMessage linked = await _client.PostAsJsonAsync("/api/v1/archives/download-link",
                new CreateArchiveDownloadLinkRequest { FileIds = [stored.Id] });
            linked.EnsureSuccessStatusCode();
            ArchiveDownloadLinkDto link = (await linked.Content.ReadFromJsonAsync<ArchiveDownloadLinkDto>())!;
            using HttpResponseMessage full = await _client.GetAsync(link.Url);
            byte[] bytes = await full.Content.ReadAsByteArrayAsync();
            int boundary = 30 + file.Name.Length + first.Length;
            using HttpResponseMessage crossing = await GetRangeAsync(link.Url, $"bytes={boundary - 7}-{boundary + 8}");
            using HttpResponseMessage tail = await GetRangeAsync(link.Url, $"bytes={boundary + 5}-", full.Headers.ETag!.ToString());
            Assert.Multiple(() =>
            {
                Assert.That(crossing.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
                Assert.That(tail.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
            });
            Assert.That(await crossing.Content.ReadAsByteArrayAsync(), Is.EqualTo(Encoding.UTF8.GetBytes("aaaaaaabbbbbbbbb")));
            Assert.That(await tail.Content.ReadAsByteArrayAsync(), Is.EqualTo(bytes.AsSpan(boundary + 5).ToArray()));
        }

        [Test]
        public async Task HeadAndRanges_DescribeAndReconstructTheSameArchive()
        {
            ArchiveDownloadLinkDto link = await CreateArchiveAsync();
            using HttpResponseMessage full = await _client!.GetAsync(link.Url);
            full.EnsureSuccessStatusCode();
            byte[] bytes = await full.Content.ReadAsByteArrayAsync();
            using HttpRequestMessage headRequest = new(HttpMethod.Head, link.Url);
            using HttpResponseMessage head = await _client.SendAsync(headRequest);
            Assert.Multiple(() =>
            {
                Assert.That(head.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(head.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/zip"));
                Assert.That(head.Content.Headers.ContentLength, Is.EqualTo(bytes.Length));
                Assert.That(head.Headers.ETag, Is.EqualTo(full.Headers.ETag));
                Assert.That(head.Headers.AcceptRanges, Does.Contain("bytes"));
                Assert.That(full.Headers.AcceptRanges, Does.Contain("bytes"));
            });
            Assert.That(await head.Content.ReadAsByteArrayAsync(), Is.Empty);

            int split = bytes.Length / 2;
            using HttpResponseMessage first = await GetRangeAsync(link.Url, $"bytes=0-{split - 1}", full.Headers.ETag!.ToString());
            using HttpResponseMessage rest = await GetRangeAsync(link.Url, $"bytes={split}-", full.Headers.ETag.ToString());
            using HttpResponseMessage suffix = await GetRangeAsync(link.Url, "bytes=-97");
            using HttpResponseMessage invalid = await GetRangeAsync(link.Url, $"bytes={bytes.Length}-");
            Assert.Multiple(() =>
            {
                Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
                Assert.That(first.Content.Headers.ContentRange?.ToString(), Is.EqualTo($"bytes 0-{split - 1}/{bytes.Length}"));
                Assert.That(first.Content.Headers.ContentLength, Is.EqualTo(split));
                Assert.That(rest.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
                Assert.That(rest.Content.Headers.ContentRange?.From, Is.EqualTo(split));
                Assert.That(suffix.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
                Assert.That(suffix.Content.Headers.ContentLength, Is.EqualTo(97));
                Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.RequestedRangeNotSatisfiable));
                Assert.That(invalid.Content.Headers.ContentRange?.ToString(), Is.EqualTo($"bytes */{bytes.Length}"));
            });
            byte[] firstBytes = await first.Content.ReadAsByteArrayAsync();
            byte[] restBytes = await rest.Content.ReadAsByteArrayAsync();
            Assert.That(firstBytes.Concat(restBytes).ToArray(), Is.EqualTo(bytes));
            Assert.That(await suffix.Content.ReadAsByteArrayAsync(), Is.EqualTo(bytes.AsSpan(bytes.Length - 97).ToArray()));
        }

        [Test]
        public async Task FreshLinkForUnchangedFiles_HasSameEntityTagAndCanResume()
        {
            ArchiveDownloadLinkDto original = await CreateArchiveAsync();
            using HttpResponseMessage full = await _client!.GetAsync(original.Url);
            byte[] bytes = await full.Content.ReadAsByteArrayAsync();
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            ArchiveDownloadLinkDto refreshed = await LinkAsync(root.Id);
            Assert.That(refreshed.Url, Is.Not.EqualTo(original.Url));
            using HttpResponseMessage resumed = await GetRangeAsync(refreshed.Url, "bytes=100-", full.Headers.ETag!.ToString());
            Assert.Multiple(() =>
            {
                Assert.That(resumed.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
                Assert.That(resumed.Headers.ETag, Is.EqualTo(full.Headers.ETag));
            });
            Assert.That(await resumed.Content.ReadAsByteArrayAsync(), Is.EqualTo(bytes.AsSpan(100).ToArray()));
        }

        [Test]
        public async Task ChangedContents_DoNotReturnPartialBytesForPreviousEntityTag()
        {
            ArchiveDownloadLinkDto link = await CreateArchiveAsync();
            using HttpResponseMessage first = await _client!.GetAsync(link.Url);
            string originalTag = first.Headers.ETag!.ToString();
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            await UploadTextFileAsync(root, "new.txt", "new contents");
            using HttpResponseMessage changed = await GetRangeAsync(link.Url, "bytes=100-", originalTag);
            Assert.Multiple(() =>
            {
                Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(changed.Headers.ETag!.ToString(), Is.Not.EqualTo(originalTag));
                Assert.That(changed.Content.Headers.ContentRange, Is.Null);
            });
            using ZipArchive archive = new(new MemoryStream(await changed.Content.ReadAsByteArrayAsync()));
            Assert.That(archive.Entries.Any(entry => entry.Name == "new.txt"), Is.True);
        }

        [Test]
        public async Task HeadAndInitialRange_DoNotReadFileContents()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            const string contents = "missing archive source";
            await UploadTextFileAsync(root, "missing.txt", contents);
            ArchiveDownloadLinkDto link = await LinkAsync(root.Id);
            IStoragePipeline storage = _factory!.Services.GetRequiredService<IStoragePipeline>();
            string hash = Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(contents)));
            Assert.That(await storage.DeleteAsync(hash), Is.True);
            using HttpRequestMessage headRequest = new(HttpMethod.Head, link.Url);
            using HttpResponseMessage head = await _client.SendAsync(headRequest);
            using HttpResponseMessage initial = await GetRangeAsync(link.Url, "bytes=0-0");
            Assert.Multiple(() =>
            {
                Assert.That(head.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(initial.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
                Assert.That(initial.Content.Headers.ContentLength, Is.EqualTo(1));
            });
            Assert.That(await initial.Content.ReadAsByteArrayAsync(), Is.EqualTo(new byte[] { 0x50 }));
        }

        private async Task<ArchiveDownloadLinkDto> CreateArchiveAsync()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            await UploadTextFileAsync(root, "первый.txt", new string('a', 4096));
            await UploadTextFileAsync(root, "second.txt", new string('b', 8192));
            return await LinkAsync(root.Id);
        }

        private async Task<ArchiveDownloadLinkDto> LinkAsync(Guid nodeId)
        {
            using HttpResponseMessage created = await _client!.PostAsJsonAsync("/api/v1/archives/download-link",
                new CreateArchiveDownloadLinkRequest { NodeIds = [nodeId] });
            created.EnsureSuccessStatusCode();
            return (await created.Content.ReadFromJsonAsync<ArchiveDownloadLinkDto>())!;
        }

        private async Task<HttpResponseMessage> GetRangeAsync(string url, string range, string? ifRange = null)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Range = RangeHeaderValue.Parse(range);
            if (ifRange is not null)
            {
                request.Headers.IfRange = new RangeConditionHeaderValue(EntityTagHeaderValue.Parse(ifRange));
            }
            return await _client!.SendAsync(request);
        }
    }
}
