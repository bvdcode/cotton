// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using Microsoft.Extensions.Configuration;

namespace Cotton.Server.IntegrationTests
{
    public class PostgresDumpRestoreTests() : IntegrationTestBase($"cotton_portable_restore_tests_{Guid.NewGuid():N}")
    {
        [SetUp]
        public async Task SetUp()
        {
            await DbContext.Database.EnsureCreatedAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            await DbContext.Database.EnsureDeletedAsync();
        }

        [Test]
        public async Task Restore_VectorArchive_PreservesArraysAndOrdinaryIndexes()
        {
            PostgresRestoreResult result = await CreateDumpService().RestoreFromFileAsync(
                Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "vector-backup.dump"));
            FileEmbedding embedding = await DbContext.FileEmbeddings.SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(new PostgresRestoreResult(true, true)));
                Assert.That(embedding.Embedding, Has.Length.EqualTo(VectorIndexDefinition.Dimensions));
                Assert.That(embedding.Embedding.All(value => value == 0.5f), Is.True);
            });
            Assert.That((await DbContext.Database.GetIndexStatusAsync("public", "file_embeddings", "ix_embedding_fragment")).Exists, Is.True);
            Assert.That((await DbContext.Database.GetIndexStatusAsync(
                VectorIndexDefinition.Expected.SchemaName, VectorIndexDefinition.Expected.TableName,
                VectorIndexDefinition.Expected.IndexName)).Exists, Is.False);
        }

        [Test]
        public async Task Restore_InvalidArchive_DoesNotChangeDatabase()
        {
            string archive = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(archive, "invalid archive");
                Assert.ThrowsAsync<InvalidOperationException>(() => CreateDumpService().RestoreFromFileAsync(archive));
                Assert.That(await DbContext.FileEmbeddings.AnyAsync(), Is.False);
            }
            finally
            {
                File.Delete(archive);
            }
        }

        [Test]
        public async Task Restore_RespectsCancellation()
        {
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();
            Assert.CatchAsync<OperationCanceledException>(() => CreateDumpService().RestoreFromFileAsync(
                Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "vector-backup.dump"), cancellation.Token));
            Assert.That(await DbContext.FileEmbeddings.AnyAsync(), Is.False);
        }

        private PostgresDumpService CreateDumpService()
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:Host"] = TestPostgresHost,
                ["DatabaseSettings:Port"] = TestPostgresPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["DatabaseSettings:Username"] = TestPostgresUsername,
                ["DatabaseSettings:Password"] = TestPostgresPassword,
                ["DatabaseSettings:Database"] = CurrentDatabaseName
            }).Build();
            return new(configuration, NullLogger<PostgresDumpService>.Instance);
        }
    }
}
