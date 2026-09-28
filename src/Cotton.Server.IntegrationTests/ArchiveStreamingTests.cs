// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Archives;
using Cotton.Server.Models.Requests;
using EasyExtensions.Mediator;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class ArchiveStreamingTests : FileEndpointTestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task RootArchive_CrossesBatchBoundary_WithoutDuplicatesOrLengthMismatch(bool duplicateNames)
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeFileManifestDto original = await UploadTextFileAsync(root, "original.txt", "contents");
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                Node node = await db.Nodes.SingleAsync(item => item.Id == root.Id);
                List<NodeFile> files = [];
                for (int i = 0; i < 270; i++)
                {
                    NodeFile file = new() { NodeId = root.Id, OwnerId = node.OwnerId, FileManifestId = original.FileManifestId };
                    file.SetName(duplicateNames ? "duplicate.txt" : $"file-{i:D4}.txt");
                    files.Add(file);
                }
                await db.NodeFiles.AddRangeAsync(files);
                await db.SaveChangesAsync();
            }

            using HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/archives/download-link",
                new CreateArchiveDownloadLinkRequest { NodeIds = [root.Id], FileIds = [original.Id] });
            created.EnsureSuccessStatusCode();
            ArchiveDownloadLinkDto link = (await created.Content.ReadFromJsonAsync<ArchiveDownloadLinkDto>())!;
            using HttpResponseMessage download = await _client.GetAsync(link.Url);
            download.EnsureSuccessStatusCode();
            byte[] bytes = await download.Content.ReadAsByteArrayAsync();
            using ZipArchive zip = new(new MemoryStream(bytes), ZipArchiveMode.Read);
            Assert.Multiple(() =>
            {
                Assert.That(zip.Entries.Count, Is.EqualTo(272));
                Assert.That(zip.Entries.Select(entry => entry.FullName).Distinct().Count(), Is.EqualTo(272));
                Assert.That(zip.Entries.Count(entry => entry.Name == "original.txt"), Is.EqualTo(1));
                Assert.That(download.Content.Headers.ContentLength, Is.EqualTo(bytes.Length));
            });
            foreach (ZipArchiveEntry entry in zip.Entries.Where(entry => !entry.FullName.EndsWith('/')))
            {
                using StreamReader reader = new(await entry.OpenAsync());
                Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("contents"));
            }
        }

        [Test]
        public async Task PreparedPlan_RemainsReadableAfterSourceRename_AndDoesNotClearCallerContext()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeFileManifestDto file = await UploadTextFileAsync(root, "original.txt", "contents");
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            NodeFile tracked = await db.NodeFiles.SingleAsync(item => item.Id == file.Id);
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            ArchiveDownloadTicket ticket = new(tracked.OwnerId, "files.zip", [file.Id], [], false);
            await using ArchiveDownloadPlan plan = new();
            await mediator.Send(new PrepareArchiveDownloadRequest(ticket, plan));
            Assert.That(db.Entry(tracked).State, Is.EqualTo(EntityState.Unchanged));
            tracked.SetName("a-much-longer-new-name.txt");
            await db.SaveChangesAsync();
            List<ArchiveDownloadEntry> entries = await plan.ReadAsync(CancellationToken.None).ToListAsync();
            Assert.That(entries.Single().Path, Is.EqualTo("original.txt"));
        }

        [Test]
        public async Task PrepareArchive_RejectsTamperedManifestBeforeWritingEntries()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeFileManifestDto file = await UploadTextFileAsync(root, "original.txt", "contents");
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            Guid ownerId = await db.NodeFiles.Where(item => item.Id == file.Id).Select(item => item.OwnerId).SingleAsync();
            await db.FileManifests.Where(item => item.Id == file.FileManifestId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SizeBytes, 100));
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            ArchiveDownloadTicket ticket = new(ownerId, "files.zip", [file.Id], [], false);
            await using ArchiveDownloadPlan plan = new();

            Assert.ThrowsAsync<DatabaseIntegrityException>(() => mediator.Send(new PrepareArchiveDownloadRequest(ticket, plan)));
            Assert.That(plan.EntryCount, Is.Zero);
        }

        [Test]
        public async Task SharedArchive_RechecksExpiredShareBeforePreparingContent()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            await UploadTextFileAsync(root, "original.txt", "contents");
            string shareLink = (await _client.GetStringAsync($"/api/v1/layouts/nodes/{root.Id}/share-link")).Trim('"');
            string token = shareLink.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
            using HttpResponseMessage created = await _client.PostAsync($"/api/v1/layouts/shared/{token}/archives/download-link", null);
            created.EnsureSuccessStatusCode();
            ArchiveDownloadLinkDto link = (await created.Content.ReadFromJsonAsync<ArchiveDownloadLinkDto>())!;
            await using (AsyncServiceScope scope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
                NodeShareToken share = await db.NodeShareTokens.SingleAsync(item => item.Token == token);
                share.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                await db.SaveChangesAsync();
            }
            _client.DefaultRequestHeaders.Authorization = null;
            using HttpResponseMessage download = await _client.GetAsync(link.Url);
            Assert.Multiple(() =>
            {
                Assert.That(download.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(download.Content.Headers.ContentType?.MediaType, Is.Not.EqualTo("application/zip"));
            });
        }

        [Test]
        public async Task ArchiveDownload_MissingChunkFailsResponseBodyInsteadOfCompletingZip()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            const string content = "archive missing chunk";
            NodeFileManifestDto file = await UploadTextFileAsync(root, "missing.txt", content);
            using HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/archives/download-link",
                new CreateArchiveDownloadLinkRequest { FileIds = [file.Id] });
            created.EnsureSuccessStatusCode();
            ArchiveDownloadLinkDto link = (await created.Content.ReadFromJsonAsync<ArchiveDownloadLinkDto>())!;
            string hash = Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(content)));
            IStoragePipeline storage = _factory!.Services.GetRequiredService<IStoragePipeline>();
            Assert.That(await storage.DeleteAsync(hash), Is.True);

            using HttpResponseMessage download = await _client.GetAsync(link.Url, HttpCompletionOption.ResponseHeadersRead);
            Assert.That(download.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/zip"));
            await using Stream body = await download.Content.ReadAsStreamAsync();
            using MemoryStream received = new();
            Assert.CatchAsync<IOException>(async () => await body.CopyToAsync(received));
            Assert.That(received.Length, Is.LessThan(download.Content.Headers.ContentLength!.Value));
        }
    }
}
