// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class StorageUsageStatsTests : IntegrationTestBase
    {
        [SetUp]
        public async Task SetUp()
        {
            await RecreateDatabaseAsync();
        }

        [Test]
        public async Task GetAsync_EmptyStorage_ReturnsZeroCountsAndSizes()
        {
            StorageUsageStatsDto stats = await GetStatsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(stats.TotalUniqueChunkCount, Is.Zero);
                Assert.That(stats.TotalUniqueChunkPlainSizeBytes, Is.Zero);
                Assert.That(stats.TotalUniqueChunkStoredSizeBytes, Is.Zero);
                AssertNoReferences(stats);
                Assert.That(stats.CompressionSavedBytes, Is.Zero);
                Assert.That(stats.PendingGcChunkCount, Is.Zero);
            });
        }

        [Test]
        public async Task GetAsync_ManifestWithoutFiles_DoesNotCountLogicalReferences()
        {
            Chunk chunk = CreateChunk("orphan", 100, 60);
            AddManifest("orphan", chunk);
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();

            StorageUsageStatsDto stats = await GetStatsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(stats.TotalUniqueChunkCount, Is.EqualTo(1));
                Assert.That(stats.TotalUniqueChunkPlainSizeBytes, Is.EqualTo(100));
                Assert.That(stats.TotalUniqueChunkStoredSizeBytes, Is.EqualTo(60));
                AssertNoReferences(stats);
                Assert.That(stats.PendingGcChunkCount, Is.Zero);
            });
        }

        [Test]
        public async Task GetAsync_CountsSharedManifestsAndRepeatedChunksWithoutMergingEqualSizes()
        {
            Chunk first = CreateChunk("first", 100, 60);
            Chunk second = CreateChunk("second", 200, 120);
            Chunk sameSize = CreateChunk("same-size", 100, 60);
            Chunk orphan = CreateChunk("orphan", 400, 240);
            Chunk garbage = CreateChunk("garbage", 500, 300);
            FileManifest shared = AddManifest("shared", first, first, second);
            FileManifest other = AddManifest("other", first, sameSize);
            AddManifest("orphan", first, orphan);
            DbContext.Chunks.Add(garbage);

            User owner = new() { Username = "storage-owner", PasswordPhc = "test", WebDavTokenPhc = "test" };
            Layout layout = new() { Owner = owner, IsActive = true };
            Node root = new() { Owner = owner, Layout = layout, Type = NodeType.Default };
            root.SetName("root");
            AddFile(root, shared, "first.bin");
            AddFile(root, shared, "copy.bin");
            AddFile(root, other, "other.bin");
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();

            StorageUsageStatsDto stats = await GetStatsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(stats.TotalUniqueChunkCount, Is.EqualTo(5));
                Assert.That(stats.TotalUniqueChunkPlainSizeBytes, Is.EqualTo(1300));
                Assert.That(stats.TotalUniqueChunkStoredSizeBytes, Is.EqualTo(780));
                Assert.That(stats.ReferencedUniqueChunkCount, Is.EqualTo(3));
                Assert.That(stats.ReferencedUniqueChunkPlainSizeBytes, Is.EqualTo(400));
                Assert.That(stats.ReferencedUniqueChunkStoredSizeBytes, Is.EqualTo(240));
                Assert.That(stats.ReferencedLogicalChunkCount, Is.EqualTo(8));
                Assert.That(stats.ReferencedLogicalPlainSizeBytes, Is.EqualTo(1000));
                Assert.That(stats.DeduplicatedUniqueChunkCount, Is.EqualTo(2));
                Assert.That(stats.DedupSavedBytes, Is.EqualTo(600));
                Assert.That(stats.CompressionSavedBytes, Is.EqualTo(520));
                Assert.That(stats.PendingGcChunkCount, Is.EqualTo(1));
                Assert.That(stats.PendingGcStoredSizeBytes, Is.EqualTo(300));
                Assert.That(stats.OverdueGcChunkCount, Is.EqualTo(1));
                Assert.That(stats.OverdueGcStoredSizeBytes, Is.EqualTo(300));
                Assert.That(DbContext.ChangeTracker.Entries(), Is.Empty);
            });
        }

        private Task<StorageUsageStatsDto> GetStatsAsync()
        {
            SettingsProvider settings = new(DbContext, new ServerSettingsCache());
            ChunkUsageService usage = new(DbContext, new InMemoryStorage(), new StaticBackupManifestService(null),
                new DatabaseBackupKeyProvider(new CottonEncryptionSettings
                {
                    MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("storage-statistics"u8))
                }), NullLogger<ChunkUsageService>.Instance);
            StorageUsageStatsService service = new(DbContext, settings, usage);
            return service.GetAsync(DateTime.UtcNow, [], CancellationToken.None);
        }

        private static void AssertNoReferences(StorageUsageStatsDto stats)
        {
            Assert.That(stats.ReferencedUniqueChunkCount, Is.Zero);
            Assert.That(stats.ReferencedUniqueChunkPlainSizeBytes, Is.Zero);
            Assert.That(stats.ReferencedUniqueChunkStoredSizeBytes, Is.Zero);
            Assert.That(stats.ReferencedLogicalChunkCount, Is.Zero);
            Assert.That(stats.ReferencedLogicalPlainSizeBytes, Is.Zero);
            Assert.That(stats.DeduplicatedUniqueChunkCount, Is.Zero);
            Assert.That(stats.DedupSavedBytes, Is.Zero);
        }

        private static Chunk CreateChunk(string name, long plainSize, long storedSize)
        {
            return new Chunk
            {
                Hash = SHA256.HashData(Encoding.UTF8.GetBytes(name)),
                PlainSizeBytes = plainSize,
                StoredSizeBytes = storedSize,
                GCScheduledAfter = DateTime.UtcNow.AddDays(-1),
            };
        }

        private FileManifest AddManifest(string name, params Chunk[] chunks)
        {
            FileManifest manifest = new()
            {
                ProposedContentHash = SHA256.HashData(Encoding.UTF8.GetBytes(name)),
                ContentType = "application/octet-stream",
                SizeBytes = chunks.Sum(chunk => chunk.PlainSizeBytes),
            };
            for (int index = 0; index < chunks.Length; index++)
            {
                manifest.FileManifestChunks.Add(new FileManifestChunk { Chunk = chunks[index], ChunkOrder = index });
            }
            DbContext.FileManifests.Add(manifest);
            return manifest;
        }

        private void AddFile(Node node, FileManifest manifest, string name)
        {
            NodeFile file = new() { Owner = node.Owner, Node = node, FileManifest = manifest };
            file.SetName(name);
            DbContext.NodeFiles.Add(file);
            file.OriginalNodeFileId = file.Id;
        }
    }
}
