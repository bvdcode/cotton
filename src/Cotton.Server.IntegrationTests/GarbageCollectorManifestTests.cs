// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Jobs;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class GarbageCollectorManifestTests : IntegrationTestBase
    {
        private DatabaseIntegrityKeyProvider _keys = null!;
        private DatabaseIntegrityProtector _protector = null!;
        private DatabaseIntegrityDescriptorRegistry _descriptors = null!;

        [OneTimeSetUp]
        public async Task CreateDatabase()
        {
            await RecreateDatabaseAsync();
            _keys = new(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("manifest-garbage-collection"u8))
            });
            _protector = new(_keys);
            _descriptors = new([new FileManifestIntegrityDescriptor(), new FileManifestChunkIntegrityDescriptor(),
                new ChunkIntegrityDescriptor(), new UserIntegrityDescriptor(), new NodeIntegrityDescriptor(), new NodeFileIntegrityDescriptor()]);
        }

        [SetUp]
        public async Task ClearDatabase()
        {
            DbContext.ChangeTracker.Clear();
            DbContext.FileEmbeddings.RemoveRange(await DbContext.FileEmbeddings.ToListAsync());
            DbContext.FileManifestChunks.RemoveRange(await DbContext.FileManifestChunks.ToListAsync());
            DbContext.NodeFiles.RemoveRange(await DbContext.NodeFiles.ToListAsync());
            DbContext.Nodes.RemoveRange(await DbContext.Nodes.ToListAsync());
            DbContext.UserLayouts.RemoveRange(await DbContext.UserLayouts.ToListAsync());
            DbContext.Users.RemoveRange(await DbContext.Users.ToListAsync());
            DbContext.FileManifests.RemoveRange(await DbContext.FileManifests.ToListAsync());
            DbContext.Chunks.RemoveRange(await DbContext.Chunks.ToListAsync());
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        [OneTimeTearDown]
        public void DisposeKeys()
        {
            _keys.Dispose();
        }

        [Test]
        public async Task RunOnce_RemovesOrphanedManifestsThroughSaveChanges_AndPreservesLiveSignatures()
        {
            var (root, manifests) = await SeedAsync();
            SaveChangesProbe probe = new();
            await using CottonDbContext context = CreateContext(probe);
            FileManifestIntegrityDescriptor descriptor = new();
            FileManifest live = await context.FileManifests.SingleAsync(manifest => manifest.Id == manifests[2].Id);
            byte[] signature = (byte[])context.Entry(live).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
            context.ChangeTracker.Clear();

            await RunAsync(context);

            Assert.That(probe.SaveCount, Is.EqualTo(1));
            Assert.That(context.ChangeTracker.HasChanges(), Is.False);
            context.ChangeTracker.Clear();
            Assert.That(await context.FileManifests.Select(manifest => manifest.Id).ToListAsync(), Is.EqualTo(new[] { live.Id }));
            Assert.That(await context.FileManifestChunks.Select(link => link.FileManifestId).ToListAsync(), Is.EqualTo(new[] { live.Id }));
            Assert.That(await context.FileEmbeddings.Select(embedding => embedding.FileManifestId).ToListAsync(), Is.EqualTo(new[] { live.Id }));
            Assert.That(await context.NodeFiles.CountAsync(), Is.EqualTo(1));
            Assert.That(await context.Chunks.CountAsync(), Is.EqualTo(1));
            FileManifest retained = await context.FileManifests.SingleAsync();
            Assert.That(context.Entry(retained).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue, Is.EqualTo(signature));
            Assert.That(_protector.Verify(retained, descriptor, signature), Is.True);

            await RunAsync(context);
            Assert.That(probe.SaveCount, Is.EqualTo(1), "Repeated cleanup must not save when there are no orphaned manifests.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RunOnce_RollsBackAllCleanup_WhenCandidateBecomesReferenced(bool duringSave)
        {
            var (root, manifests) = await SeedAsync();
            Guid[] manifestIds = manifests.Select(manifest => manifest.Id).ToArray();
            bool referenceCreated = false;
            async Task CreateReference(CancellationToken ct)
            {
                Assert.That(referenceCreated, Is.False);
                await using CottonDbContext writer = CreateContext();
                NodeFile file = new() { OwnerId = root.OwnerId, NodeId = root.Id, FileManifestId = manifests[0].Id };
                file.SetName("restored.txt");
                writer.NodeFiles.Add(file);
                file.OriginalNodeFileId = file.Id;
                await writer.SaveChangesAsync(ct);
                referenceCreated = true;
            }
            SaveChangesProbe saveProbe = new(duringSave ? CreateReference : null);
            ManifestLinksDeletedInterceptor deletionProbe = new(CreateReference);
            IInterceptor[] interceptors = duringSave ? [saveProbe] : [saveProbe, deletionProbe];
            await using CottonDbContext context = CreateContext(interceptors);
            FileManifestIntegrityDescriptor manifestDescriptor = new();
            FileManifestChunkIntegrityDescriptor linkDescriptor = new();
            List<FileManifest> beforeManifests = await context.FileManifests.ToListAsync();
            List<FileManifestChunk> beforeLinks = await context.FileManifestChunks.ToListAsync();
            List<FileEmbedding> beforeEmbeddings = await context.FileEmbeddings.AsNoTracking().ToListAsync();
            Dictionary<Guid, byte[]> manifestSignatures = beforeManifests.ToDictionary(manifest => manifest.Id,
                manifest => (byte[])context.Entry(manifest).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!);
            Dictionary<Guid, byte[]> linkSignatures = beforeLinks.ToDictionary(link => link.Id,
                link => (byte[])context.Entry(link).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!);
            context.ChangeTracker.Clear();

            await RunAsync(context);

            Assert.Multiple(() =>
            {
                Assert.That(referenceCreated, Is.True);
                Assert.That(deletionProbe.Invoked, Is.EqualTo(!duringSave));
                Assert.That(saveProbe.SaveCount, Is.EqualTo(duringSave ? 1 : 0));
                Assert.That(context.ChangeTracker.HasChanges(), Is.False, "A rolled-back cleanup must not leave pending deletions.");
            });
            context.ChangeTracker.Clear();
            List<FileManifest> retainedManifests = await context.FileManifests.ToListAsync();
            List<FileManifestChunk> retainedLinks = await context.FileManifestChunks.ToListAsync();
            List<FileEmbedding> retainedEmbeddings = await context.FileEmbeddings.ToListAsync();
            Assert.That(retainedManifests.Select(manifest => manifest.Id), Is.EquivalentTo(manifestIds));
            Assert.That(retainedLinks.Select(link => link.Id), Is.EquivalentTo(beforeLinks.Select(link => link.Id)));
            Assert.That(retainedEmbeddings.Select(embedding => embedding.Id), Is.EquivalentTo(beforeEmbeddings.Select(embedding => embedding.Id)));
            Assert.That(await context.NodeFiles.CountAsync(), Is.EqualTo(2));
            Assert.That(await context.Chunks.CountAsync(), Is.EqualTo(1));
            foreach (FileManifest manifest in retainedManifests)
            {
                Assert.That(context.Entry(manifest).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue,
                    Is.EqualTo(manifestSignatures[manifest.Id]));
                Assert.That(_protector.Verify(manifest, manifestDescriptor, manifestSignatures[manifest.Id]), Is.True);
            }
            foreach (FileManifestChunk link in retainedLinks)
            {
                Assert.That(context.Entry(link).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue,
                    Is.EqualTo(linkSignatures[link.Id]));
                Assert.That(_protector.Verify(link, linkDescriptor, linkSignatures[link.Id]), Is.True);
            }
        }

        private CottonDbContext CreateContext(params IInterceptor[] interceptors)
        {
            DatabaseIntegrityChangeSigner signer = new(_protector, _descriptors, NullDatabaseIntegrityFailureReporter.Instance);
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(TestDataSource);
            options.AddInterceptors(new DatabaseIntegritySaveChangesInterceptor(signer));
            options.AddInterceptors(interceptors);
            return new(options.Options);
        }

        private async Task<(Node Root, FileManifest[] Manifests)> SeedAsync()
        {
            await using CottonDbContext context = CreateContext();
            User owner = new() { Username = "manifestowner", PasswordPhc = "test", WebDavTokenPhc = "test" };
            Layout layout = new() { Owner = owner, IsActive = true };
            Node root = new() { Owner = owner, Layout = layout, Type = NodeType.Default };
            root.SetName("root");
            context.Nodes.Add(root);
            Chunk chunk = new() { Hash = SHA256.HashData("shared-manifest-chunk"u8), PlainSizeBytes = 100, StoredSizeBytes = 100 };
            FileManifest[] manifests = Enumerable.Range(0, 3).Select(_ => new FileManifest
            {
                ProposedContentHash = SHA256.HashData(Guid.NewGuid().ToByteArray()), SizeBytes = 100
            }).ToArray();
            foreach (FileManifest manifest in manifests)
            {
                context.FileManifests.Add(manifest);
                context.Entry(manifest).Property<string>("ContentType").CurrentValue = string.Empty;
                context.FileManifestChunks.Add(new FileManifestChunk { FileManifest = manifest, Chunk = chunk, ChunkOrder = 0 });
                context.FileEmbeddings.Add(new FileEmbedding { FileManifest = manifest, IndexVersion = 1, Embedding = [0.1f, 0.2f] });
            }
            NodeFile live = new() { Owner = owner, Node = root, FileManifest = manifests[2] };
            live.SetName("live.txt");
            context.NodeFiles.Add(live);
            live.OriginalNodeFileId = live.Id;
            await context.SaveChangesAsync();
            return (root, manifests);
        }

        private static async Task RunAsync(CottonDbContext context)
        {
            InMemoryStorage storage = new();
            DatabaseBackupKeyProvider keys = new(new CottonEncryptionSettings { MasterEncryptionKey = "test-master-key" });
            ChunkUsageService usage = new(context, storage, new StaticBackupManifestService(null), keys, NullLogger<ChunkUsageService>.Instance);
            SettingsProvider settings = new(context, new ServerSettingsCache());
            using ServiceProvider services = new ServiceCollection().AddSingleton(settings).BuildServiceProvider();
            using DatabaseBackupGate gate = new();
            GarbageCollectorJob job = new(new PerfTracker(services.GetRequiredService<IServiceScopeFactory>()), storage,
                context, usage, gate, settings, NullLogger<GarbageCollectorJob>.Instance);
            await job.RunOnceAsync(DateTime.UtcNow, batchSize: 0);
        }
    }
}
