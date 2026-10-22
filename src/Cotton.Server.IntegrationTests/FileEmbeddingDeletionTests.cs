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
    public class FileEmbeddingDeletionTests : IntegrationTestBase
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
            DbContext.FileEmbeddings.RemoveRange(await DbContext.FileEmbeddings.ToListAsync());
            DbContext.FileManifests.RemoveRange(await DbContext.FileManifests.ToListAsync());
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task DeleteFileEmbeddings_PreservesScopeSignaturesAndTransaction(bool emptySelection, bool rollBack)
        {
            using DatabaseIntegrityKeyProvider keys = new(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("embedding-deletion"u8))
            });
            DatabaseIntegrityProtector protector = new(keys);
            FileManifestIntegrityDescriptor descriptor = new();
            DatabaseIntegrityDescriptorRegistry descriptors = new([descriptor]);
            DatabaseIntegrityChangeSigner signer = new(protector, descriptors, NullDatabaseIntegrityFailureReporter.Instance);
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(TestDataSource);
            options.AddInterceptors(new DatabaseIntegritySaveChangesInterceptor(signer));
            await using CottonDbContext context = new(options.Options);
            FileManifest[] manifests = [CreateManifest(), CreateManifest(), CreateManifest()];
            context.FileManifests.AddRange(manifests);
            foreach (FileManifest manifest in manifests)
            {
                context.Entry(manifest).Property<string>("ContentType").CurrentValue = string.Empty;
                context.FileEmbeddings.AddRange(
                    CreateEmbedding(manifest, 0, 1), CreateEmbedding(manifest, 1, 1), CreateEmbedding(manifest, 0, 2));
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            List<FileEmbedding> before = await context.FileEmbeddings.AsNoTracking().ToListAsync();
            List<FileManifest> beforeManifests = await context.FileManifests.ToListAsync();
            Dictionary<Guid, byte[]> signatures = beforeManifests.ToDictionary(manifest => manifest.Id,
                manifest => (byte[])context.Entry(manifest).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!);
            Dictionary<Guid, byte[]> payloads = beforeManifests.ToDictionary(manifest => manifest.Id, descriptor.BuildCanonicalPayload);
            context.ChangeTracker.Clear();
            Guid[] selectedIds = emptySelection ? [] : [manifests[0].Id, manifests[1].Id];
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

            int deleted = await context.FileEmbeddings.DeleteFileEmbeddingsAsync(selectedIds, CancellationToken.None);
            int repeated = await context.FileEmbeddings.DeleteFileEmbeddingsAsync(selectedIds, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(deleted, Is.EqualTo(before.Count(embedding => selectedIds.Contains(embedding.FileManifestId))));
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

            List<FileEmbedding> remaining = await context.FileEmbeddings.AsNoTracking().ToListAsync();
            Assert.That(remaining.Select(embedding => embedding.Id), Is.EquivalentTo(
                before.Where(embedding => rollBack || !selectedIds.Contains(embedding.FileManifestId)).Select(embedding => embedding.Id)));
            foreach (FileEmbedding embedding in remaining)
            {
                FileEmbedding original = before.Single(item => item.Id == embedding.Id);
                Assert.Multiple(() =>
                {
                    Assert.That(embedding.FileManifestId, Is.EqualTo(original.FileManifestId));
                    Assert.That(embedding.FragmentIndex, Is.EqualTo(original.FragmentIndex));
                    Assert.That(embedding.IndexVersion, Is.EqualTo(original.IndexVersion));
                    Assert.That(embedding.Embedding, Is.EqualTo(original.Embedding));
                    Assert.That(embedding.CreatedAt, Is.EqualTo(original.CreatedAt));
                    Assert.That(embedding.UpdatedAt, Is.EqualTo(original.UpdatedAt));
                });
            }
            List<FileManifest> remainingManifests = await context.FileManifests.ToListAsync();
            Assert.That(remainingManifests.Select(manifest => manifest.Id), Is.EquivalentTo(beforeManifests.Select(manifest => manifest.Id)));
            foreach (FileManifest manifest in remainingManifests)
            {
                byte[] signature = (byte[])context.Entry(manifest).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
                Assert.Multiple(() =>
                {
                    Assert.That(signature, Is.EqualTo(signatures[manifest.Id]));
                    Assert.That(descriptor.BuildCanonicalPayload(manifest), Is.EqualTo(payloads[manifest.Id]));
                    Assert.That(context.Entry(manifest).Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                        Is.EqualTo(descriptor.SchemaVersion));
                    Assert.That(protector.Verify(manifest, descriptor, signature), Is.True);
                });
            }
        }

        private static FileManifest CreateManifest()
        {
            return new FileManifest { ProposedContentHash = SHA256.HashData(Guid.NewGuid().ToByteArray()), SizeBytes = 100 };
        }

        private static FileEmbedding CreateEmbedding(FileManifest manifest, int fragmentIndex, int indexVersion)
        {
            return new FileEmbedding
            {
                FileManifest = manifest,
                FragmentIndex = fragmentIndex,
                IndexVersion = indexVersion,
                Embedding = [0.1f, 0.2f, 0.3f]
            };
        }
    }
}
