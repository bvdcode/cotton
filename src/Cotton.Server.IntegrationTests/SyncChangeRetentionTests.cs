// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Handlers.Sync;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Jobs;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Cotton.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Triggers;
using Quartz.Spi;
using System.Security.Cryptography;
using System.Text.Json;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class SyncChangeRetentionTests : IntegrationTestBase
    {
        private DatabaseIntegrityKeyProvider _keys = null!;
        private DatabaseIntegrityProtector _protector = null!;
        private readonly UserIntegrityDescriptor _userDescriptor = new();

        [OneTimeSetUp]
        public async Task CreateDatabase()
        {
            await RecreateDatabaseAsync();
            _keys = new(new CottonEncryptionSettings
            {
                MasterEncryptionKey = Convert.ToBase64String(SHA256.HashData("sync-retention-contract"u8))
            });
            _protector = new(_keys);
        }

        [SetUp]
        public async Task ClearDatabase()
        {
            DbContext.ChangeTracker.Clear();
            DbContext.SyncChanges.RemoveRange(await DbContext.SyncChanges.ToListAsync());
            DbContext.Users.RemoveRange(await DbContext.Users.ToListAsync());
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        [OneTimeTearDown]
        public void DisposeKeys()
        {
            _keys.Dispose();
        }

        [Test]
        public void SyncChanges_HaveNoIntegritySignature()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DbContext.Model.FindEntityType(typeof(SyncChange)), Is.Not.Null);
                Assert.That(DbContext.Model.FindEntityType(typeof(SyncChange))!
                    .FindProperty(DatabaseIntegrityColumns.VersionProperty), Is.Null);
                Assert.That(DbContext.Model.FindEntityType(typeof(SyncChange))!
                    .FindProperty(DatabaseIntegrityColumns.MacProperty), Is.Null);
            });
        }

        [Test]
        public async Task DeleteExpiredChanges_PreservesOwnerMarkersBoundaryRowsAndSignatures()
        {
            DateTime cutoff = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            User firstOwner = await AddOwnerAsync();
            User expiredOnlyOwner = await AddOwnerAsync();
            User singleExpiredOwner = await AddOwnerAsync();
            User boundaryOwner = await AddOwnerAsync();
            long removedFirst = await AddChangeAsync(firstOwner, cutoff.AddDays(-3));
            long removedSecond = await AddChangeAsync(firstOwner, cutoff.AddDays(-1));
            long firstMarker = await AddChangeAsync(firstOwner, cutoff.AddDays(-2));
            await AddChangeAsync(firstOwner, cutoff.AddDays(1));
            long removedOtherOwner = await AddChangeAsync(expiredOnlyOwner, cutoff.AddDays(-2));
            long expiredOnlyMarker = await AddChangeAsync(expiredOnlyOwner, cutoff.AddDays(-1));
            await AddChangeAsync(singleExpiredOwner, cutoff.AddDays(-1));
            await AddChangeAsync(singleExpiredOwner, cutoff.AddDays(1));
            await AddChangeAsync(boundaryOwner, cutoff);
            await AddChangeAsync(boundaryOwner, cutoff);
            await AddChangeAsync(boundaryOwner, cutoff.AddDays(1));
            long[] removedIds = [removedFirst, removedSecond, removedOtherOwner];
            List<SyncChange> before = await DbContext.SyncChanges.AsNoTracking().OrderBy(change => change.Id).ToListAsync();
            Dictionary<Guid, byte[]> signatures = await DbContext.Users
                .Select(user => new { user.Id, Mac = EF.Property<byte[]>(user, DatabaseIntegrityColumns.MacProperty) })
                .ToDictionaryAsync(user => user.Id, user => user.Mac);
            DbContext.ChangeTracker.Clear();
            SyncChangeRetentionJob job = CreateJob();

            int deleted = await job.DeleteExpiredChangesAsync(cutoff, CancellationToken.None);
            int repeated = await job.DeleteExpiredChangesAsync(cutoff, CancellationToken.None);
            List<SyncChange> remaining = await DbContext.SyncChanges.AsNoTracking().OrderBy(change => change.Id).ToListAsync();
            GetSyncChangesQueryHandler reader = new(DbContext);
            SyncChangesResponseDto feed = await reader.Handle(new(expiredOnlyOwner.Id, 0, 10), CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(deleted, Is.EqualTo(removedIds.Length));
                Assert.That(repeated, Is.Zero);
                Assert.That(DbContext.ChangeTracker.Entries(), Is.Empty);
                Assert.That(JsonSerializer.Serialize(remaining),
                    Is.EqualTo(JsonSerializer.Serialize(before.Where(change => !removedIds.Contains(change.Id)))));
                Assert.That(remaining.Any(change => change.Id == firstMarker), Is.True);
                Assert.That(feed.CursorExpired, Is.True);
                Assert.That(feed.EarliestAvailableCursor, Is.EqualTo(expiredOnlyMarker - 1));
                Assert.That(feed.Changes, Is.Empty);
            });
            foreach (User user in await DbContext.Users.ToListAsync())
            {
                byte[] mac = (byte[])DbContext.Entry(user).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
                Assert.That(mac, Is.EqualTo(signatures[user.Id]));
                Assert.That(_protector.Verify(user, _userDescriptor, mac), Is.True);
            }
        }

        [Test]
        public async Task Execute_ExpiresChangesAfterThirtyDays()
        {
            User owner = await AddOwnerAsync();
            DateTime now = DateTime.UtcNow;
            await AddChangeAsync(owner, now.AddDays(-32));
            long marker = await AddChangeAsync(owner, now.AddDays(-31));
            long recent = await AddChangeAsync(owner, now.AddDays(-29));
            DbContext.ChangeTracker.Clear();
            SyncChangeRetentionJob job = CreateJob();
            TriggerFiredBundle bundle = new(JobBuilder.Create<SyncChangeRetentionJob>().Build(),
                new SimpleTriggerImpl(), null, false, DateTimeOffset.UtcNow, null, null, null);
            JobExecutionContextImpl context = new(null!, bundle, job);

            await job.Execute(context);

            Assert.That(await DbContext.SyncChanges.OrderBy(change => change.Id).Select(change => change.Id).ToListAsync(),
                Is.EqualTo(new[] { marker, recent }));
        }

        [Test]
        public async Task DeleteExpiredChanges_CancellationPreservesRows()
        {
            User owner = await AddOwnerAsync();
            DateTime cutoff = DateTime.UtcNow.AddDays(-30);
            long first = await AddChangeAsync(owner, cutoff.AddDays(-2));
            long second = await AddChangeAsync(owner, cutoff.AddDays(-1));
            DbContext.ChangeTracker.Clear();
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();

            Assert.That(async () => await CreateJob().DeleteExpiredChangesAsync(cutoff, cancellation.Token),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(await DbContext.SyncChanges.OrderBy(change => change.Id).Select(change => change.Id).ToListAsync(),
                Is.EqualTo(new[] { first, second }));
        }

        private SyncChangeRetentionJob CreateJob()
        {
            return new(DbContext, NullLogger<SyncChangeRetentionJob>.Instance);
        }

        private async Task<User> AddOwnerAsync()
        {
            User user = new()
            {
                Username = "retention" + Guid.NewGuid().ToString("N")[..8],
                PasswordPhc = "test-password",
                WebDavTokenPhc = "test-webdav-token"
            };
            DbContext.Users.Add(user);
            DbContext.Entry(user).Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = _userDescriptor.SchemaVersion;
            DbContext.Entry(user).Property(DatabaseIntegrityColumns.MacProperty).CurrentValue = _protector.Sign(user, _userDescriptor);
            await DbContext.SaveChangesAsync();
            return user;
        }

        private async Task<long> AddChangeAsync(User owner, DateTime createdAt)
        {
            SyncChange change = new()
            {
                OwnerId = owner.Id,
                Kind = SyncChangeKind.FileCreated,
                LayoutId = Guid.NewGuid(),
                ItemId = Guid.NewGuid(),
                ParentNodeId = Guid.NewGuid(),
                Name = "retention.txt"
            };
            DbContext.SyncChanges.Add(change);
            await DbContext.SaveChangesAsync();
            await DbContext.SyncChanges.Where(row => row.Id == change.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.CreatedAt, createdAt)
                    .SetProperty(row => row.UpdatedAt, createdAt));
            DbContext.Entry(change).State = EntityState.Detached;
            return change.Id;
        }
    }
}
