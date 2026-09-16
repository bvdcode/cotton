// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Extensions;
using Cotton.Server.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class PostgresExtensionExtensionsTests
    {
        [Test]
        public async Task EnsurePostgresExtensionAsync_CreatesOnlyRequestedRestoreExtensions()
        {
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused")
                .ReplaceService<IMigrationCommandExecutor, RecordingMigrationCommandExecutor>()
                .Options;
            await using CottonDbContext context = new(options);

            await context.Database.EnsurePostgresExtensionAsync("citext", CancellationToken.None);
            await context.Database.EnsurePostgresExtensionAsync("hstore", CancellationToken.None);

            RecordingMigrationCommandExecutor executor =
                (RecordingMigrationCommandExecutor)context.GetService<IMigrationCommandExecutor>();
            Assert.That(executor.Commands.Select(command => command.Trim()), Is.EqualTo(new[]
            {
                "CREATE EXTENSION IF NOT EXISTS citext;",
                "CREATE EXTENSION IF NOT EXISTS hstore;"
            }));
            Assert.That(context.ChangeTracker.Entries(), Is.Empty);
        }
    }
}
