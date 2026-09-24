// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Services;
using Cotton.Server.Services.Search;
using Cotton.TextExtraction;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;
using System.Globalization;

namespace Cotton.Server.IntegrationTests
{
    public partial class FileTextIndexingTests
    {
        [Test]
        public async Task Backup_Restore_PreservesSchemaAndFilesWithoutEmbeddings()
        {
            string[] paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator);
            string dumpExecutable = OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump";
            string restoreExecutable = OperatingSystem.IsWindows() ? "pg_restore.exe" : "pg_restore";
            if (!paths.Any(path => File.Exists(Path.Combine(path, dumpExecutable)))
                || !paths.Any(path => File.Exists(Path.Combine(path, restoreExecutable))))
            {
                Assert.Ignore("PostgreSQL dump and restore tools must be available on PATH.");
            }
            await PrepareVectorIndexAsync();
            FileManifest manifest = await AddFileAsync("backup.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Backed up document"));
            await IndexAsync(manifest.Id);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.True);

            string dumpPath = Path.Combine(Path.GetTempPath(), $"cotton-backup-test-{Guid.NewGuid():N}.dump");
            string restoreDatabase = $"cotton_restore_tests_{Guid.NewGuid():N}";
            NpgsqlConnectionStringBuilder connection = new(_db.Database.GetConnectionString()) { Database = restoreDatabase };
            await using CottonDbContext restored = new(new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql(connection.ConnectionString).Options);
            try
            {
                await CreateDumpService(CurrentDatabaseName).DumpToFileAsync(dumpPath);
                await restored.Database.EnsureCreatedAsync();
                await CreateDumpService(restoreDatabase).RestoreFromFileAsync(dumpPath);
                await restored.Database.OpenConnectionAsync();
                await ((NpgsqlConnection)restored.Database.GetDbConnection()).ReloadTypesAsync();

                Assert.That(await restored.FileEmbeddings.AnyAsync(), Is.False);
                FileManifest stored = await restored.FileManifests.SingleAsync();
                Assert.That(stored.Id, Is.EqualTo(manifest.Id));
                Assert.That(stored.TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
                Assert.That(await restored.NodeFiles.CountAsync(), Is.EqualTo(1));
                Assert.That(await restored.Chunks.CountAsync(), Is.EqualTo(1));
                PostgresIndexStatus index = await restored.Database.GetIndexStatusAsync(
                    VectorIndexDefinition.Expected.SchemaName, VectorIndexDefinition.Expected.TableName,
                    VectorIndexDefinition.Expected.IndexName);
                Assert.That(VectorIndexDefinition.IsReady(index), Is.True);
            }
            finally
            {
                await restored.Database.CloseConnectionAsync();
                await restored.Database.EnsureDeletedAsync();
                File.Delete(dumpPath);
            }
        }

        private static PostgresDumpService CreateDumpService(string databaseName)
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:Host"] = TestPostgresHost,
                ["DatabaseSettings:Port"] = TestPostgresPort.ToString(CultureInfo.InvariantCulture),
                ["DatabaseSettings:Username"] = TestPostgresUsername,
                ["DatabaseSettings:Password"] = TestPostgresPassword,
                ["DatabaseSettings:Database"] = databaseName,
            }).Build();
            return new PostgresDumpService(configuration, NullLogger<PostgresDumpService>.Instance);
        }
    }
}
