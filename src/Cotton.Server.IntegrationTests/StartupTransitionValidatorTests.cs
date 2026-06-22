// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Helpers;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.Services.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class StartupTransitionValidatorTests : IntegrationTestBase
    {
        private string? _previousAppVersion;

        public StartupTransitionValidatorTests()
            : base("cotton_dev_tests_startup_guard_" + Guid.NewGuid().ToString("N"))
        {
        }

        [SetUp]
        public void SetUp()
        {
            _previousAppVersion = Environment.GetEnvironmentVariable(AppVersionHelpers.AppVersionEnvironmentVariable);
            Environment.SetEnvironmentVariable(AppVersionHelpers.AppVersionEnvironmentVariable, null);

            NpgsqlConnection.ClearAllPools();
            var creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();
            NpgsqlConnection.ClearAllPools();
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(AppVersionHelpers.AppVersionEnvironmentVariable, _previousAppVersion);
            NpgsqlConnection.ClearAllPools();
            DbContext.GetService<IRelationalDatabaseCreator>().EnsureDeleted();
            NpgsqlConnection.ClearAllPools();
        }

        [Test]
        public async Task ValidateAsync_AllowsEmptyDatabase()
        {
            SetCurrentVersion("0.5.0");

            StartupBlocker? blocker = await CreateValidator().ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Null);
        }

        [Test]
        public async Task ValidateAsync_BlocksStableUpgradeWithoutRequiredTransitionVersion()
        {
            SetCurrentVersion("0.5.0");
            await MigrateAndRecordVersionAsync("0.4.32");

            StartupBlocker? blocker = await CreateValidator().ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(blocker!.Kind, Is.EqualTo("version-transition-required"));
                Assert.That(blocker.CurrentVersion, Is.EqualTo("0.5.0"));
                Assert.That(blocker.RequiredVersion, Is.EqualTo("0.4.33"));
                Assert.That(blocker.LastRecordedVersion, Is.EqualTo("0.4.32"));
            }
        }

        [Test]
        public async Task ValidateAsync_AllowsStableUpgradeWithRequiredTransitionVersion()
        {
            SetCurrentVersion("0.5.0");
            await MigrateAndRecordVersionAsync("0.4.33", DateTime.UtcNow.AddHours(-25));

            StartupBlocker? blocker = await CreateValidator().ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Null);
        }

        [Test]
        public async Task ValidateAsync_BlocksStableUpgradeWhenRequiredTransitionVersionIsTooRecent()
        {
            SetCurrentVersion("0.5.0");
            await MigrateAndRecordVersionAsync("0.4.33", DateTime.UtcNow.AddHours(-23));

            StartupBlocker? blocker = await CreateValidator().ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(blocker!.Kind, Is.EqualTo("version-transition-required"));
                Assert.That(blocker.RequiredVersion, Is.EqualTo("0.4.33"));
                Assert.That(blocker.LastRecordedVersion, Is.EqualTo("0.4.33"));
            }
        }

        [Test]
        public async Task ValidateAsync_DoesNotApplyStableRuleToDevelopPrerelease()
        {
            SetCurrentVersion("0.5.0-alpha.482");
            await MigrateAndRecordVersionAsync("0.4.32");

            StartupBlocker? blocker = await CreateValidator().ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Null);
        }

        private async Task MigrateAndRecordVersionAsync(string version, DateTime? createdAt = null)
        {
            await DbContext.Database.MigrateAsync();
            AppVersion appVersion = new()
            {
                Version = version,
            };
            DbContext.AppVersions.Add(appVersion);
            if (createdAt.HasValue)
            {
                DbContext.Entry(appVersion).Property(nameof(AppVersion.CreatedAt)).CurrentValue = createdAt.Value;
            }
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        private static void SetCurrentVersion(string version)
        {
            Environment.SetEnvironmentVariable(AppVersionHelpers.AppVersionEnvironmentVariable, version);
        }

        private StartupTransitionValidator CreateValidator()
        {
            return new StartupTransitionValidator(
                DbContext,
                NullLogger<StartupTransitionValidator>.Instance);
        }
    }
}
