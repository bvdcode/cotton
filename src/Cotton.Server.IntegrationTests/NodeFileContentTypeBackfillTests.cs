// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests
{
    public class NodeFileContentTypeBackfillTests : IntegrationTestBase
    {
        private DatabaseIntegrityKeyProvider _keys = null!;
        private DatabaseIntegrityProtector _protector = null!;
        private DatabaseIntegrityDescriptorRegistry _descriptors = null!;
        private DatabaseIntegrityVerifier _verifier = null!;

        [OneTimeSetUp]
        public async Task CreateDatabase()
        {
            await RecreateDatabaseAsync();
            _keys = new(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("backfill-test-key"u8))
            });
            _protector = new(_keys);
            _descriptors = new([new NodeFileIntegrityDescriptor()]);
            _verifier = new(_protector, _descriptors, NullDatabaseIntegrityFailureReporter.Instance,
                NullLogger<DatabaseIntegrityVerifier>.Instance);
        }

        [SetUp]
        public async Task ClearDatabase()
        {
            DbContext.ChangeTracker.Clear();
            DbContext.NodeFiles.RemoveRange(await DbContext.NodeFiles.ToListAsync());
            DbContext.FileManifests.RemoveRange(await DbContext.FileManifests.ToListAsync());
            DbContext.Nodes.RemoveRange(await DbContext.Nodes.ToListAsync());
            DbContext.UserLayouts.RemoveRange(await DbContext.UserLayouts.ToListAsync());
            DbContext.Users.RemoveRange(await DbContext.Users.ToListAsync());
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _keys.Dispose();
        }

        [Test]
        public async Task Backfill_SavesMultipleBatches_AndLeavesCurrentFilesAndRepeatRunsUnchanged()
        {
            NodeFile[] files = await SeedFilesAsync(1003, 1, string.Empty);
            NodeFile current = files[^1];
            current.SetName("current.txt");
            await DbContext.SaveChangesAsync();
            await SetSignatureAsync(current, 2);
            DbContext.ChangeTracker.Clear();
            Dictionary<Guid, NodeFile> before = await DbContext.NodeFiles.AsNoTracking().ToDictionaryAsync(file => file.Id);
            SaveChangesProbe probe = new();
            await using CottonDbContext context = CreateSignedContext(probe);
            BackfillNodeFileContentTypesRequestHandler handler = CreateHandler(context);

            Assert.That(await handler.Handle(new(), CancellationToken.None), Is.EqualTo(1002));
            Assert.That(probe.SaveCount, Is.EqualTo(3));
            List<NodeFile> actual = await context.NodeFiles.ToListAsync();
            Assert.Multiple(() =>
            {
                foreach (NodeFile file in actual)
                {
                    Assert.That(file.ContentType, Is.EqualTo(file.Id == current.Id ? "text/plain" : "text/markdown"));
                    Assert.That(file.CreatedAt, Is.EqualTo(before[file.Id].CreatedAt));
                    if (file.Id == current.Id)
                    {
                        Assert.That(file.UpdatedAt, Is.EqualTo(before[file.Id].UpdatedAt));
                    }
                    else
                    {
                        Assert.That(file.UpdatedAt, Is.GreaterThan(before[file.Id].UpdatedAt));
                    }
                    _verifier.RequireValid(context, file, "test.backfilled-file");
                }
            });
            Dictionary<Guid, DateTime> dates = actual.ToDictionary(file => file.Id, file => file.UpdatedAt);
            context.ChangeTracker.Clear();
            Assert.That(await handler.Handle(new(), CancellationToken.None), Is.Zero);
            Assert.That(probe.SaveCount, Is.EqualTo(3));
            Assert.That(await context.NodeFiles.AsNoTracking().ToDictionaryAsync(file => file.Id, file => file.UpdatedAt),
                Is.EquivalentTo(dates));
        }

        [TestCase(1, false, false)]
        [TestCase(1, true, false)]
        [TestCase(2, false, false)]
        [TestCase(1, false, true)]
        [TestCase(1, true, true)]
        [TestCase(2, false, true)]
        public async Task Backfill_UpgradesLegacySignatures_AndRejectsTamperedFiles(int version, bool populated, bool tampered)
        {
            string initialType = populated ? "application/custom" : string.Empty;
            NodeFile[] files = await SeedFilesAsync(2, version, initialType);
            Dictionary<Guid, NodeFile> before = await DbContext.NodeFiles.AsNoTracking().ToDictionaryAsync(file => file.Id);
            Guid lastId = files[^1].Id;
            if (tampered)
            {
                await DbContext.NodeFiles.Where(file => file.Id == lastId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.Name, "tampered.md"));
            }
            await using CottonDbContext context = CreateSignedContext(new SaveChangesProbe());
            BackfillNodeFileContentTypesRequestHandler handler = CreateHandler(context);
            if (tampered)
            {
                await Assert.ThrowsAsync<DatabaseIntegrityException>(() => handler.Handle(new(), CancellationToken.None));
                Assert.That(await DbContext.NodeFiles.AsNoTracking().Select(file => file.ContentType).ToListAsync(),
                    Is.All.EqualTo(initialType));
                Assert.That(await DbContext.NodeFiles.Select(file => EF.Property<int?>(file, DatabaseIntegrityColumns.VersionProperty)).ToListAsync(),
                    Is.All.EqualTo(version));
                return;
            }

            Assert.That(await handler.Handle(new(), CancellationToken.None), Is.EqualTo(files.Length));
            List<NodeFile> actual = await context.NodeFiles.ToListAsync();
            foreach (NodeFile file in actual)
            {
                Assert.That(file.CreatedAt, Is.EqualTo(before[file.Id].CreatedAt));
                Assert.That(file.UpdatedAt, Is.GreaterThan(before[file.Id].UpdatedAt));
                Assert.That(file.ContentType, Is.EqualTo(populated ? initialType : "text/markdown"));
                Assert.That(context.Entry(file).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue,
                    Is.EqualTo(NodeFileIntegrityDescriptor.LatestVersion));
                _verifier.RequireValid(context, file, "test.signature-upgrade");
            }
            Dictionary<Guid, DateTime> dates = actual.ToDictionary(file => file.Id, file => file.UpdatedAt);
            context.ChangeTracker.Clear();
            Assert.That(await handler.Handle(new(), CancellationToken.None), Is.Zero);
            Assert.That(await context.NodeFiles.AsNoTracking().ToDictionaryAsync(file => file.Id, file => file.UpdatedAt),
                Is.EquivalentTo(dates));
        }

        [Test]
        public async Task Backfill_RejectsConcurrentRename_AndRollsBackTheBatch()
        {
            NodeFile[] files = await SeedFilesAsync(2, 1, string.Empty);
            Guid lastId = files[^1].Id;
            await using CottonDbContext concurrent = CreateSignedContext(new SaveChangesProbe());
            SaveChangesProbe probe = new(async cancellationToken =>
            {
                NodeFile file = await concurrent.NodeFiles.SingleAsync(file => file.Id == lastId, cancellationToken);
                file.SetName("renamed.pdf");
                await concurrent.SaveChangesAsync(cancellationToken);
            });
            await using CottonDbContext context = CreateSignedContext(probe);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => CreateHandler(context).Handle(new(), CancellationToken.None));
            DbContext.ChangeTracker.Clear();
            NodeFile untouched = await DbContext.NodeFiles.SingleAsync(file => file.Id == files[0].Id);
            NodeFile renamed = await DbContext.NodeFiles.SingleAsync(file => file.Id == lastId);
            Assert.Multiple(() =>
            {
                Assert.That(untouched.ContentType, Is.Empty);
                Assert.That(DbContext.Entry(untouched).Property<int?>(DatabaseIntegrityColumns.VersionProperty).CurrentValue, Is.EqualTo(1));
                Assert.That(renamed.Name, Is.EqualTo("renamed.pdf"));
                Assert.That(renamed.ContentType, Is.EqualTo("application/pdf"));
            });
            _verifier.RequireValid(DbContext, untouched, "test.rolled-back-file");
            _verifier.RequireValid(DbContext, renamed, "test.concurrent-rename");
        }

        private CottonDbContext CreateSignedContext(SaveChangesProbe probe)
        {
            DatabaseIntegrityChangeSigner signer = new(_protector, _descriptors, NullDatabaseIntegrityFailureReporter.Instance);
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(TestDataSource);
            options.AddInterceptors(probe, new DatabaseIntegritySaveChangesInterceptor(signer));
            return new CottonDbContext(options.Options);
        }

        private BackfillNodeFileContentTypesRequestHandler CreateHandler(CottonDbContext context)
        {
            return new(context, _descriptors,
                NullLogger<BackfillNodeFileContentTypesRequestHandler>.Instance);
        }

        private async Task<NodeFile[]> SeedFilesAsync(int count, int version, string contentType)
        {
            User user = new() { Username = "backfill", PasswordPhc = "test", WebDavTokenPhc = "test" };
            Layout layout = new() { Owner = user, IsActive = true };
            Node node = new() { Owner = user, Layout = layout };
            node.SetName("root");
            FileManifest manifest = new() { ProposedContentHash = SHA256.HashData("shared-content"u8), ContentType = string.Empty };
            NodeFile[] files = Enumerable.Range(0, count).Select(index =>
            {
                NodeFile file = new() { Owner = user, Node = node, FileManifest = manifest };
                file.SetName($"legacy-{index}.md");
                return file;
            }).ToArray();
            DbContext.Users.Add(user);
            DbContext.NodeFiles.AddRange(files);
            foreach (NodeFile file in files)
            {
                file.OriginalNodeFileId = file.Id;
                DbContext.Entry(file).Property(entity => entity.ContentType).CurrentValue = contentType;
                using HMACSHA256 hmac = _keys.CreateHmac();
                byte[] signature = hmac.ComputeHash(_descriptors.Get<NodeFile>(version).BuildCanonicalPayload(file));
                DbContext.Entry(file).Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = version;
                DbContext.Entry(file).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue = signature;
            }
            await DbContext.SaveChangesAsync();
            return files.OrderBy(file => file.Id).ToArray();
        }

        private async Task SetSignatureAsync(NodeFile file, int version)
        {
            using HMACSHA256 hmac = _keys.CreateHmac();
            byte[] signature = hmac.ComputeHash(_descriptors.Get<NodeFile>(version).BuildCanonicalPayload(file));
            await DbContext.NodeFiles.Where(entity => entity.Id == file.Id).ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => EF.Property<int?>(entity, DatabaseIntegrityColumns.VersionProperty), version)
                .SetProperty(entity => EF.Property<byte[]?>(entity, DatabaseIntegrityColumns.MacProperty), signature));
        }
    }
}
