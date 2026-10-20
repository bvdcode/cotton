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
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    public class ApprovedBulkMutationExtensionsTests : IntegrationTestBase
    {
        [SetUp]
        public async Task CreateDatabase()
        {
            await DbContext.Database.EnsureDeletedAsync();
            await DbContext.Database.EnsureCreatedAsync();
        }

        [Test]
        public void ChunkGcSchedule_IsExcludedFromActualSignature()
        {
            Chunk chunk = CreateChunk("schedule-contract");
            ChunkIntegrityDescriptor descriptor = new();
            byte[] originalPayload = descriptor.BuildCanonicalPayload(chunk);

            chunk.GCScheduledAfter = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(descriptor.BuildCanonicalPayload(chunk), Is.EqualTo(originalPayload),
                "GC scheduling may bypass the signer only while GCScheduledAfter is excluded from the signed payload.");

            chunk.StoredSizeBytes++;
            Assert.That(descriptor.BuildCanonicalPayload(chunk), Is.Not.EqualTo(originalPayload),
                "The contract must use the real descriptor with protected fields, not an empty signature payload.");
        }

        [Test]
        public async Task ScheduleGarbageCollection_PreservesScopeExistingDatesAndSignatures()
        {
            DateTime existingDeadline = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime newDeadline = existingDeadline.AddDays(7);
            Chunk target = CreateChunk("target");
            Chunk alreadyScheduled = CreateChunk("already-scheduled");
            alreadyScheduled.GCScheduledAfter = existingDeadline;
            Chunk excluded = CreateChunk("excluded");
            Chunk[] chunks = [target, alreadyScheduled, excluded];
            using DatabaseIntegrityKeyProvider keyProvider = new(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("gc-scheduling-contract"u8))
            });
            DatabaseIntegrityProtector protector = new(keyProvider);
            ChunkIntegrityDescriptor descriptor = new();
            Dictionary<string, byte[]> originalPayloads = chunks.ToDictionary(
                chunk => Convert.ToHexString(chunk.Hash), descriptor.BuildCanonicalPayload);
            Dictionary<string, byte[]> originalSignatures = chunks.ToDictionary(
                chunk => Convert.ToHexString(chunk.Hash), chunk => protector.Sign(chunk, descriptor));

            DbContext.Chunks.AddRange(chunks);
            foreach (Chunk chunk in chunks)
            {
                DbContext.Entry(chunk).Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = descriptor.SchemaVersion;
                DbContext.Entry(chunk).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue =
                    originalSignatures[Convert.ToHexString(chunk.Hash)];
            }
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();

            byte[][] selectedHashes = [target.Hash, alreadyScheduled.Hash];
            IQueryable<Chunk> selected = DbContext.Chunks.Where(chunk => selectedHashes.Contains(chunk.Hash));
            int updated = await selected.ScheduleGarbageCollectionAsync(newDeadline, CancellationToken.None);
            int repeated = await selected.ScheduleGarbageCollectionAsync(newDeadline.AddDays(1), CancellationToken.None);

            List<Chunk> actual = await DbContext.Chunks.ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(updated, Is.EqualTo(1));
                Assert.That(repeated, Is.Zero);
                Assert.That(actual, Has.Count.EqualTo(chunks.Length));
                Assert.That(actual.Single(chunk => chunk.Hash.SequenceEqual(target.Hash)).GCScheduledAfter, Is.EqualTo(newDeadline));
                Assert.That(actual.Single(chunk => chunk.Hash.SequenceEqual(alreadyScheduled.Hash)).GCScheduledAfter, Is.EqualTo(existingDeadline));
                Assert.That(actual.Single(chunk => chunk.Hash.SequenceEqual(excluded.Hash)).GCScheduledAfter, Is.Null);
                foreach (Chunk chunk in actual)
                {
                    string key = Convert.ToHexString(chunk.Hash);
                    byte[] storedSignature = (byte[])DbContext.Entry(chunk).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
                    Assert.That(descriptor.BuildCanonicalPayload(chunk), Is.EqualTo(originalPayloads[key]),
                        "An approved bulk mutation must not change any data included in the actual signature.");
                    Assert.That(storedSignature, Is.EqualTo(originalSignatures[key]));
                    Assert.That(DbContext.Entry(chunk).Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                        Is.EqualTo(descriptor.SchemaVersion));
                    Assert.That(protector.Verify(chunk, descriptor, storedSignature), Is.True);
                }
            });
        }

        private static Chunk CreateChunk(string value)
        {
            return new Chunk
            {
                Hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)),
                PlainSizeBytes = 4096,
                StoredSizeBytes = 2048
            };
        }
    }
}
