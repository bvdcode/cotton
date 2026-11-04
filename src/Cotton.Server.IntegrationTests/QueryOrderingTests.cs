// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Server;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Providers;
using Cotton.Topology;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class QueryOrderingTests : IntegrationTestBase
    {
        private CottonDbContext _queryContext = null!;

        [SetUp]
        public async Task SetUp()
        {
            await RecreateDatabaseAsync();
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(TestDataSource);
            options.ConfigureWarnings(warnings => warnings.Throw(
                CoreEventId.FirstWithoutOrderByAndFilterWarning,
                CoreEventId.RowLimitingOperationWithoutOrderByWarning));
            _queryContext = new(options.Options);
        }

        [TearDown]
        public async Task TearDown() => await _queryContext.DisposeAsync();

        [TestCase(0)]
        [TestCase(3)]
        public async Task Statistics_ReturnCorrectAggregatesWithoutOrderingWarnings(int count)
        {
            DateTime now = DateTime.UtcNow;
            for (int index = 0; index < count; index++)
            {
                DbContext.Chunks.Add(new Chunk
                {
                    Hash = SHA256.HashData(BitConverter.GetBytes(index)),
                    PlainSizeBytes = 100,
                    StoredSizeBytes = 60,
                    GCScheduledAfter = now.AddDays(-2),
                });
            }
            await DbContext.SaveChangesAsync();
            SettingsProvider settings = new(_queryContext, new ServerSettingsCache());
            ChunkUsageService usage = new(_queryContext, new InMemoryStorage(), new StaticBackupManifestService(null),
                new DatabaseBackupKeyProvider(new CottonEncryptionSettings
                {
                    MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("query-ordering"u8)),
                }), NullLogger<ChunkUsageService>.Instance);
            StorageUsageStatsService statistics = new(_queryContext, settings, usage);
            GetGcChunksTimelineQueryHandler handler = new(_queryContext, settings, usage, statistics);

            GcChunkTimelineDto result = await handler.Handle(
                new(now.AddDays(-1), now.AddDays(1), "day", "UTC"), CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.TotalChunks, Is.EqualTo(count));
                Assert.That(result.TotalSizeBytes, Is.EqualTo(count * 60));
                Assert.That(result.Storage.TotalUniqueChunkCount, Is.EqualTo(count));
                Assert.That(result.Storage.TotalUniqueChunkPlainSizeBytes, Is.EqualTo(count * 100));
                Assert.That(result.Storage.ReferencedUniqueChunkCount, Is.Zero);
                Assert.That(result.Storage.PendingGcChunkCount, Is.EqualTo(count));
                Assert.That(result.Storage.OverdueGcChunkCount, Is.EqualTo(count));
            });
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task Traversal_ReturnsOnlyRequestedLevelWithoutOrderingWarnings(int depth)
        {
            User owner = new() { Username = "ordering-owner", PasswordPhc = "test", WebDavTokenPhc = "test" };
            Layout layout = new() { Owner = owner, IsActive = true };
            Node root = new() { Owner = owner, Layout = layout, Type = NodeType.Default };
            root.SetName("root");
            Node child = CreateChild(root, "child");
            Node leaf = CreateChild(child, "leaf");
            Node excluded = CreateChild(root, "trash");
            excluded.Type = NodeType.Trash;
            DbContext.Nodes.AddRange(root, child, leaf, excluded);
            await DbContext.SaveChangesAsync();

            List<Guid> ids = await DirectoryListing.ResolveParentIdsAsync(
                new NodeDirectory(_queryContext, root), root.Id, depth, CancellationToken.None);
            Guid expected = depth switch
            {
                0 => root.Id,
                1 => child.Id,
                2 => leaf.Id,
                _ => throw new ArgumentOutOfRangeException(nameof(depth)),
            };

            Assert.That(ids, Is.EqualTo(new[] { expected }));
        }

        private static Node CreateChild(Node parent, string name)
        {
            Node node = new() { Owner = parent.Owner, Layout = parent.Layout, Parent = parent, Type = parent.Type };
            node.SetName(name);
            return node;
        }
    }
}
