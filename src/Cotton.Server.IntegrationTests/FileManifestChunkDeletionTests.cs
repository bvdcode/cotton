// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class FileManifestChunkDeletionTests : IntegrationTestBase
    {
        [OneTimeSetUp]
        public async Task CreateDatabase()
        {
            await DbContext.Database.EnsureDeletedAsync();
            await DbContext.Database.EnsureCreatedAsync();
        }

        [SetUp]
        public async Task ClearDatabase()
        {
            DbContext.ChangeTracker.Clear();
            DbContext.FileManifestChunks.RemoveRange(await DbContext.FileManifestChunks.ToListAsync());
            DbContext.FileManifests.RemoveRange(await DbContext.FileManifests.ToListAsync());
            DbContext.Chunks.RemoveRange(await DbContext.Chunks.ToListAsync());
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task DeleteFileManifestChunks_PreservesSharedChunksSignaturesAndTransaction(bool emptySelection, bool rollBack)
        {
            using DatabaseIntegrityKeyProvider keys = new(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("manifest-chunk-deletion"u8))
            });
            DatabaseIntegrityProtector protector = new(keys);
            FileManifestIntegrityDescriptor manifestDescriptor = new();
            FileManifestChunkIntegrityDescriptor linkDescriptor = new();
            ChunkIntegrityDescriptor chunkDescriptor = new();
            DatabaseIntegrityDescriptorRegistry descriptors = new([manifestDescriptor, linkDescriptor, chunkDescriptor]);
            DatabaseIntegrityChangeSigner signer = new(protector, descriptors, NullDatabaseIntegrityFailureReporter.Instance);
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(DbContext.Database.GetConnectionString());
            options.AddInterceptors(new DatabaseIntegritySaveChangesInterceptor(signer));
            await using CottonDbContext context = new(options.Options);
            FileManifest[] manifests = [CreateManifest(), CreateManifest(), CreateManifest()];
            Chunk shared = CreateChunk();
            Chunk selectedOnly = CreateChunk();
            Chunk retainedOnly = CreateChunk();
            context.FileManifests.AddRange(manifests);
            for (int index = 0; index < manifests.Length; index++)
            {
                FileManifest manifest = manifests[index];
                context.Entry(manifest).Property<string>("ContentType").CurrentValue = string.Empty;
                context.FileManifestChunks.AddRange(
                    new FileManifestChunk { FileManifest = manifest, Chunk = shared, ChunkOrder = 0 },
                    new FileManifestChunk { FileManifest = manifest, Chunk = index < 2 ? selectedOnly : retainedOnly, ChunkOrder = 1 });
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            List<FileManifestChunk> beforeLinks = await context.FileManifestChunks.ToListAsync();
            List<FileManifest> beforeManifests = await context.FileManifests.ToListAsync();
            List<Chunk> beforeChunks = await context.Chunks.ToListAsync();
            Dictionary<string, (byte[] Payload, byte[] Signature)> linkSnapshots = Capture(context, beforeLinks, linkDescriptor);
            Dictionary<string, (byte[] Payload, byte[] Signature)> manifestSnapshots = Capture(context, beforeManifests, manifestDescriptor);
            Dictionary<string, (byte[] Payload, byte[] Signature)> chunkSnapshots = Capture(context, beforeChunks, chunkDescriptor);
            context.ChangeTracker.Clear();
            Guid[] selectedIds = emptySelection ? [] : [manifests[0].Id, manifests[1].Id];
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

            int deleted = await context.FileManifestChunks.DeleteFileManifestChunksAsync(selectedIds, CancellationToken.None);
            int repeated = await context.FileManifestChunks.DeleteFileManifestChunksAsync(selectedIds, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(deleted, Is.EqualTo(beforeLinks.Count(link => selectedIds.Contains(link.FileManifestId))));
                Assert.That(repeated, Is.Zero);
            });
            if (rollBack)
            {
                await transaction.RollbackAsync();
            }
            else
            {
                await transaction.CommitAsync();
            }

            List<FileManifestChunk> remainingLinks = await context.FileManifestChunks.ToListAsync();
            List<FileManifest> remainingManifests = await context.FileManifests.ToListAsync();
            List<Chunk> remainingChunks = await context.Chunks.ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(remainingLinks.Select(link => link.Id), Is.EquivalentTo(
                    beforeLinks.Where(link => rollBack || !selectedIds.Contains(link.FileManifestId)).Select(link => link.Id)));
                Assert.That(remainingManifests.Select(manifest => manifest.Id), Is.EquivalentTo(beforeManifests.Select(manifest => manifest.Id)));
                Assert.That(remainingChunks.Select(chunk => Convert.ToHexString(chunk.Hash)), Is.EquivalentTo(
                    beforeChunks.Select(chunk => Convert.ToHexString(chunk.Hash))));
            });
            VerifyUnchanged(context, remainingLinks, linkDescriptor, linkSnapshots, protector);
            VerifyUnchanged(context, remainingManifests, manifestDescriptor, manifestSnapshots, protector);
            VerifyUnchanged(context, remainingChunks, chunkDescriptor, chunkSnapshots, protector);
            foreach (FileManifestChunk link in remainingLinks)
            {
                FileManifestChunk original = beforeLinks.Single(item => item.Id == link.Id);
                Assert.That((link.CreatedAt, link.UpdatedAt), Is.EqualTo((original.CreatedAt, original.UpdatedAt)));
            }
            foreach (Chunk chunk in remainingChunks)
            {
                Chunk original = beforeChunks.Single(item => item.Hash.SequenceEqual(chunk.Hash));
                Assert.That(chunk.GCScheduledAfter, Is.EqualTo(original.GCScheduledAfter));
            }
        }

        private static Dictionary<string, (byte[] Payload, byte[] Signature)> Capture<T>(
            CottonDbContext context, IEnumerable<T> entities, IDatabaseIntegrityDescriptor<T> descriptor) where T : class
        {
            return entities.ToDictionary(descriptor.GetEntityKey, entity => (
                descriptor.BuildCanonicalPayload(entity),
                (byte[])context.Entry(entity).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!));
        }

        private static void VerifyUnchanged<T>(CottonDbContext context, IEnumerable<T> entities,
            IDatabaseIntegrityDescriptor<T> descriptor, Dictionary<string, (byte[] Payload, byte[] Signature)> snapshots,
            DatabaseIntegrityProtector protector) where T : class
        {
            foreach (T entity in entities)
            {
                var (payload, originalSignature) = snapshots[descriptor.GetEntityKey(entity)];
                byte[] signature = (byte[])context.Entry(entity).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
                Assert.Multiple(() =>
                {
                    Assert.That(signature, Is.EqualTo(originalSignature));
                    Assert.That(descriptor.BuildCanonicalPayload(entity), Is.EqualTo(payload));
                    Assert.That(context.Entry(entity).Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                        Is.EqualTo(descriptor.SchemaVersion));
                    Assert.That(protector.Verify(entity, descriptor, signature), Is.True);
                });
            }
        }

        private static FileManifest CreateManifest()
        {
            return new FileManifest { ProposedContentHash = SHA256.HashData(Guid.NewGuid().ToByteArray()), SizeBytes = 100 };
        }

        private static Chunk CreateChunk()
        {
            return new Chunk
            {
                Hash = SHA256.HashData(Guid.NewGuid().ToByteArray()),
                PlainSizeBytes = 100,
                StoredSizeBytes = 80,
                GCScheduledAfter = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
        }
    }
}
