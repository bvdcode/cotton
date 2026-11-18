// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Services.Search;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
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
        public async Task Dump_StreamsBinaryArchiveWithoutSeeking()
        {
            string header = await CreateDumpService().ReadDumpAsync(async (stream, token) =>
            {
                Assert.That(stream.CanSeek, Is.False);
                byte[] prefix = new byte[5];
                await stream.ReadExactlyAsync(prefix, token);
                await stream.CopyToAsync(Stream.Null, token);
                return Encoding.ASCII.GetString(prefix);
            });
            Assert.That(header, Is.EqualTo("PGDMP"));
        }

        [Test]
        public async Task Dump_ConsumerFailure_StopsProducer()
        {
            HashSet<int> existing = GetDumpProcessIds();
            IOException failure = new("Storage write failed.");
            IOException? actual = await Assert.ThrowsAsync<IOException>(() => CreateDumpService()
                .ReadDumpAsync<int>((stream, token) => Task.FromException<int>(failure))
                .WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.That(actual, Is.SameAs(failure));
            Assert.That(GetDumpProcessIds().Except(existing), Is.Empty);
        }

        [Test]
        public async Task Dump_CancellationDuringConsumption_StopsProducer()
        {
            HashSet<int> existing = GetDumpProcessIds();
            using CancellationTokenSource cancellation = new();
            await Assert.CatchAsync<OperationCanceledException>(() => CreateDumpService().ReadDumpAsync(async (stream, token) =>
            {
                byte[] prefix = new byte[5];
                await stream.ReadExactlyAsync(prefix, token);
                await cancellation.CancelAsync();
                await Task.Delay(Timeout.Infinite, token);
                return 0;
            }, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.That(GetDumpProcessIds().Except(existing), Is.Empty);
        }

        [Test]
        public async Task Dump_FailedProcess_DoesNotReturnConsumerResult()
        {
            InvalidOperationException? failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateDumpService($"missing_{Guid.NewGuid():N}").ReadDumpAsync(async (stream, token) =>
                {
                    await stream.CopyToAsync(Stream.Null, token);
                    return 123;
                }));
            Assert.That(failure!.Message, Does.Contain("pg_dump failed with exit code"));
        }

        [Test]
        public async Task Dump_PreCanceled_DoesNotStartProducer()
        {
            HashSet<int> existing = GetDumpProcessIds();
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();
            await Assert.CatchAsync<OperationCanceledException>(() => CreateDumpService()
                .ReadDumpAsync((stream, token) => Task.FromResult(0), cancellation.Token));
            Assert.That(GetDumpProcessIds().Except(existing), Is.Empty);
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
                await Assert.ThrowsAsync<InvalidOperationException>(() => CreateDumpService().RestoreFromFileAsync(archive));
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
            await Assert.CatchAsync<OperationCanceledException>(() => CreateDumpService().RestoreFromFileAsync(
                Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "vector-backup.dump"), cancellation.Token));
            Assert.That(await DbContext.FileEmbeddings.AnyAsync(), Is.False);
        }

        private static HashSet<int> GetDumpProcessIds()
        {
            HashSet<int> ids = [];
            foreach (Process process in Process.GetProcessesByName("pg_dump"))
            {
                using (process)
                {
                    ids.Add(process.Id);
                }
            }
            return ids;
        }

        private PostgresDumpService CreateDumpService(string? databaseName = null)
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:Host"] = TestPostgresHost,
                ["DatabaseSettings:Port"] = TestPostgresPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["DatabaseSettings:Username"] = TestPostgresUsername,
                ["DatabaseSettings:Password"] = TestPostgresPassword,
                ["DatabaseSettings:Database"] = databaseName ?? CurrentDatabaseName
            }).Build();
            return new(configuration, NullLogger<PostgresDumpService>.Instance);
        }
    }
}
