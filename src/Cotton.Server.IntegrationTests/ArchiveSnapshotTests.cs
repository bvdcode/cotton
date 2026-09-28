// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Archives;
using EasyExtensions.Mediator;
using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class ArchiveSnapshotTests : FileEndpointTestBase
    {
        [Test]
        public async Task ArchivePlan_KeepsOriginalTreeAcrossBatchesWhileConcurrentChangesCommit()
        {
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync());
            NodeDto root = (await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver"))!;
            NodeDto folder = await CreateFolderAsync(root.Id, "export");
            NodeDto nested = await CreateFolderAsync(folder.Id, "nested");
            NodeDto outside = await CreateFolderAsync(root.Id, "outside");
            const string originalContent = "original contents";
            NodeFileManifestDto original = await UploadTextFileAsync(folder, "file-0000.txt", originalContent);
            NodeFileManifestDto child = await UploadTextFileAsync(nested, "child.txt", originalContent);
            NodeFileManifestDto replacement = await UploadTextFileAsync(outside, "replacement.txt", "replacement body");
            const int fileCount = 270;
            Guid ownerId;
            await using (AsyncServiceScope seedScope = _factory!.Services.CreateAsyncScope())
            {
                CottonDbContext db = seedScope.ServiceProvider.GetRequiredService<CottonDbContext>();
                ownerId = await db.Nodes.Where(node => node.Id == folder.Id).Select(node => node.OwnerId).SingleAsync();
                for (int i = 1; i < fileCount; i++)
                {
                    NodeFile file = new() { OwnerId = ownerId, NodeId = folder.Id, FileManifestId = original.FileManifestId };
                    file.SetName($"file-{i:D4}.txt");
                    db.NodeFiles.Add(file);
                }
                await db.SaveChangesAsync();
            }

            await using AsyncServiceScope readerScope = _factory!.Services.CreateAsyncScope();
            IMediator mediator = readerScope.ServiceProvider.GetRequiredService<IMediator>();
            ArchiveDownloadTicket ticket = new(ownerId, "export.zip", [], [folder.Id], false);
            IAsyncEnumerable<ArchiveDownloadEntry> entries = await mediator.Send(new ReadArchiveEntriesQuery(ticket));
            await using ArchiveDownloadPlan plan = new();
            bool changed = false;
            await foreach (ArchiveDownloadEntry entry in entries)
            {
                await plan.AppendAsync(entry, CancellationToken.None);
                if (entry.Path == "export/file-0255.txt")
                {
                    await ChangeTreeAsync();
                    changed = true;
                }
            }

            List<ArchiveDownloadEntry> snapshot = await plan.ReadAsync(CancellationToken.None).ToListAsync();
            List<string> expectedPaths = ["export/", "export/nested/", "export/nested/child.txt"];
            expectedPaths.AddRange(Enumerable.Range(0, fileCount).Select(i => $"export/file-{i:D4}.txt"));
            string originalHash = Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(originalContent)));
            Assert.Multiple(() =>
            {
                Assert.That(changed, Is.True);
                Assert.That(snapshot.Select(entry => entry.Path), Is.EquivalentTo(expectedPaths));
                Assert.That(snapshot.OfType<ArchiveDownloadFileEntry>().All(file =>
                    file.SizeBytes == Encoding.UTF8.GetByteCount(originalContent)
                    && file.ChunkHashes.SequenceEqual(new[] { originalHash })), Is.True);
            });

            async Task ChangeTreeAsync()
            {
                await using AsyncServiceScope writeScope = _factory.Services.CreateAsyncScope();
                CottonDbContext db = writeScope.ServiceProvider.GetRequiredService<CottonDbContext>();
                List<NodeFile> files = await db.NodeFiles.Where(file => file.NodeId == folder.Id).ToListAsync();
                files.Single(file => file.Id == original.Id).SetName("zzz.txt");
                files.Single(file => file.Name == "file-0260.txt").SetName("aaa.txt");
                files.Single(file => file.Name == "file-0261.txt").NodeId = outside.Id;
                files.Single(file => file.Name == "file-0262.txt").FileManifestId = replacement.FileManifestId;
                db.NodeFiles.Remove(files.Single(file => file.Name == "file-0263.txt"));
                NodeFile added = new() { OwnerId = ownerId, NodeId = folder.Id, FileManifestId = replacement.FileManifestId };
                added.SetName("new.txt");
                db.NodeFiles.Add(added);
                Node nestedNode = await db.Nodes.SingleAsync(node => node.Id == nested.Id);
                Node outsideNode = await db.Nodes.SingleAsync(node => node.Id == outside.Id);
                nestedNode.SetName("renamed");
                nestedNode.SetParent(outsideNode);
                NodeFile childFile = await db.NodeFiles.SingleAsync(file => file.Id == child.Id);
                childFile.SetName("renamed-child.txt");
                await db.SaveChangesAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
    }
}
