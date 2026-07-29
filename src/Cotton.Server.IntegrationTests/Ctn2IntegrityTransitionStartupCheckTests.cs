// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.DatabaseIntegrity.Descriptors;
using Cotton.Server.Services.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
#pragma warning disable CS0618 // OBSOLETE TRANSITION: these tests intentionally cover the temporary 0.5 guard.
    [NonParallelizable]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class Ctn2IntegrityTransitionStartupCheckTests : IntegrationTestBase
    {
        public Ctn2IntegrityTransitionStartupCheckTests()
            : base("cotton_dev_tests_startup_guard_" + Guid.NewGuid().ToString("N"))
        {
        }

        [SetUp]
        public void SetUp()
        {
            NpgsqlConnection.ClearAllPools();
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            DbContext.Database.Migrate();
            NpgsqlConnection.ClearAllPools();
        }

        [TearDown]
        public void TearDown()
        {
            NpgsqlConnection.ClearAllPools();
            DbContext.GetService<IRelationalDatabaseCreator>().EnsureDeleted();
            NpgsqlConnection.ClearAllPools();
        }

        [Test]
        public async Task ValidateAsync_AllowsEmptyDatabase()
        {
            var storage = new InMemoryStorage();

            StartupBlocker? blocker = await CreateCheck(storage).ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Null);
        }

        [Test]
        public async Task ValidateAsync_BlocksUnsignedProtectedRows()
        {
            var storage = new InMemoryStorage();
            await AddChunkAsync(signed: false);

            StartupBlocker? blocker = await CreateCheck(storage).ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(blocker!.Kind, Is.EqualTo("ctn2-integrity-transition-required"));
                Assert.That(blocker.RequiredVersion, Is.EqualTo("0.4.35"));
                Assert.That(blocker.Message, Does.Contain("1 protected database rows"));
            }
        }

        [Test]
        public async Task ValidateAsync_BlocksMissingStorageCompletionMarker()
        {
            var storage = new InMemoryStorage();
            await AddChunkAsync(signed: true);

            StartupBlocker? blocker = await CreateCheck(storage).ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker!.Message, Does.Contain("completion marker is missing"));
        }

        [Test]
        public async Task ValidateAsync_AllowsCompletedTransition()
        {
            var storage = new InMemoryStorage();
            await AddChunkAsync(signed: true);
            await using var marker = new MemoryStream([1]);
            await storage.WriteAsync(Ctn2IntegrityTransitionState.CompletionStorageMarkerKey, marker);

            StartupBlocker? blocker = await CreateCheck(storage).ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Null);
        }

        private async Task AddChunkAsync(bool signed)
        {
            var chunk = new Chunk
            {
                Hash = Hasher.HashData([1, 2, 3]),
                PlainSizeBytes = 3,
                StoredSizeBytes = 3,
            };
            DbContext.Chunks.Add(chunk);
            if (signed)
            {
                EntityEntry<Chunk> entry = DbContext.Entry(chunk);
                entry.Property(DatabaseIntegrityColumns.VersionProperty).CurrentValue = 1;
                entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue = new byte[32];
            }
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        private Ctn2IntegrityTransitionStartupCheck CreateCheck(InMemoryStorage storage)
        {
            var descriptors = new DatabaseIntegrityDescriptorRegistry([new ChunkIntegrityDescriptor()]);
            var diagnostics = new DatabaseIntegrityDiagnosticsService(DbContext, descriptors);
            return new Ctn2IntegrityTransitionStartupCheck(
                DbContext,
                diagnostics,
                storage,
                NullLogger<Ctn2IntegrityTransitionStartupCheck>.Instance);
        }
    }
#pragma warning restore CS0618
}
