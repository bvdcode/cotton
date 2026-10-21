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
    public class ChunkOwnershipDeletionTests : IntegrationTestBase
    {
        [OneTimeSetUp]
        public async Task CreateDatabase()
        {
            await RecreateDatabaseAsync();
        }

        [SetUp]
        public async Task ClearDatabase()
        {
            DbContext.ChangeTracker.Clear();
            DbContext.ChunkOwnerships.RemoveRange(await DbContext.ChunkOwnerships.ToListAsync());
            DbContext.Users.RemoveRange(await DbContext.Users.ToListAsync());
            DbContext.Chunks.RemoveRange(await DbContext.Chunks.ToListAsync());
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task DeleteChunkOwnerships_PreservesScopeSignaturesAndTransaction(bool emptySelection, bool rollBack)
        {
            using DatabaseIntegrityKeyProvider keys = new(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("chunk-ownership-deletion"u8))
            });
            DatabaseIntegrityProtector protector = new(keys);
            UserIntegrityDescriptor ownerDescriptor = new();
            ChunkIntegrityDescriptor chunkDescriptor = new();
            DatabaseIntegrityDescriptorRegistry descriptors = new([ownerDescriptor, chunkDescriptor]);
            DatabaseIntegrityChangeSigner signer = new(protector, descriptors, NullDatabaseIntegrityFailureReporter.Instance);
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(TestDataSource);
            options.AddInterceptors(new DatabaseIntegritySaveChangesInterceptor(signer));
            await using CottonDbContext context = new(options.Options);
            User[] owners =
            [
                new User { Username = "first-owner", PasswordPhc = "test", WebDavTokenPhc = "test" },
                new User { Username = "second-owner", PasswordPhc = "test", WebDavTokenPhc = "test" }
            ];
            Chunk[] chunks = [CreateChunk(), CreateChunk(), CreateChunk()];
            foreach (User owner in owners)
            {
                foreach (Chunk chunk in chunks)
                {
                    context.ChunkOwnerships.Add(new ChunkOwnership { Owner = owner, Chunk = chunk });
                }
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            List<ChunkOwnership> beforeOwnerships = await context.ChunkOwnerships.AsNoTracking().ToListAsync();
            List<User> beforeOwners = await context.Users.ToListAsync();
            List<Chunk> beforeChunks = await context.Chunks.ToListAsync();
            Dictionary<string, (byte[] Payload, byte[] Signature)> ownerSnapshots = Capture(context, beforeOwners, ownerDescriptor);
            Dictionary<string, (byte[] Payload, byte[] Signature)> chunkSnapshots = Capture(context, beforeChunks, chunkDescriptor);
            context.ChangeTracker.Clear();
            byte[][] selectedHashes = emptySelection ? [] :
                [chunks[0].Hash.ToArray(), chunks[1].Hash.ToArray(), chunks[0].Hash.ToArray(), SHA256.HashData("unknown-chunk"u8)];
            HashSet<string> selectedKeys = selectedHashes.Select(Convert.ToHexString).ToHashSet();
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

            int deleted = await context.ChunkOwnerships.DeleteChunkOwnershipsAsync(selectedHashes, CancellationToken.None);
            int repeated = await context.ChunkOwnerships.DeleteChunkOwnershipsAsync(selectedHashes, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(deleted, Is.EqualTo(beforeOwnerships.Count(ownership => selectedKeys.Contains(Convert.ToHexString(ownership.ChunkHash)))));
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

            List<ChunkOwnership> remaining = await context.ChunkOwnerships.AsNoTracking().ToListAsync();
            List<User> remainingOwners = await context.Users.ToListAsync();
            List<Chunk> remainingChunks = await context.Chunks.ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(remaining.Select(ownership => ownership.Id), Is.EquivalentTo(beforeOwnerships
                    .Where(ownership => rollBack || !selectedKeys.Contains(Convert.ToHexString(ownership.ChunkHash)))
                    .Select(ownership => ownership.Id)));
                Assert.That(remainingOwners.Select(owner => owner.Id), Is.EquivalentTo(beforeOwners.Select(owner => owner.Id)));
                Assert.That(remainingChunks.Select(chunk => Convert.ToHexString(chunk.Hash)), Is.EquivalentTo(
                    beforeChunks.Select(chunk => Convert.ToHexString(chunk.Hash))));
            });
            foreach (ChunkOwnership ownership in remaining)
            {
                ChunkOwnership original = beforeOwnerships.Single(item => item.Id == ownership.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(ownership.OwnerId, Is.EqualTo(original.OwnerId));
                    Assert.That(ownership.ChunkHash, Is.EqualTo(original.ChunkHash));
                    Assert.That((ownership.CreatedAt, ownership.UpdatedAt), Is.EqualTo((original.CreatedAt, original.UpdatedAt)));
                });
            }
            VerifyUnchanged(context, remainingOwners, ownerDescriptor, ownerSnapshots, protector);
            VerifyUnchanged(context, remainingChunks, chunkDescriptor, chunkSnapshots, protector);
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
