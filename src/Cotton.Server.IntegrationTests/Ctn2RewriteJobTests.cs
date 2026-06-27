// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Jobs;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Processors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class Ctn2RewriteJobTests : IntegrationTestBase
    {
        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            DbContext.Database.Migrate();
        }

        [Test]
        public async Task RunOnce_WhenStorageMarkerExists_SkipsAndCreatesTempMarkerAndClearsGcSchedule()
        {
            var storage = new InMemoryStorage();
            await WriteStorageMarkerAsync(storage);
            await AddScheduledMarkerChunkAsync();
            using AesGcmStreamCipher cipher = CreateCipher();
            var job = CreateJob(storage, cipher);
            string tempMarkerPath = job.GetCompletionMarkerPathForTests();
            DeleteTempMarker(tempMarkerPath);

            try
            {
                await job.RunOnceAsync();
                DbContext.ChangeTracker.Clear();

                Chunk markerChunk = (await DbContext.Chunks.FindAsync(Ctn2RewriteJob.CompletionStorageMarkerHash))!;
                bool storageMarkerExists = await storage.ExistsAsync(Ctn2RewriteJob.CompletionStorageMarkerKey);

                Assert.Multiple(() =>
                {
                    Assert.That(storageMarkerExists, Is.True);
                    Assert.That(File.Exists(tempMarkerPath), Is.True);
                    Assert.That(markerChunk.GCScheduledAfter, Is.Null);
                });
            }
            finally
            {
                DeleteTempMarker(tempMarkerPath);
            }
        }

        [Test]
        public async Task RunOnce_WhenTempMarkerExists_SkipsAndCreatesStorageMarkerAndClearsGcSchedule()
        {
            var storage = new InMemoryStorage();
            await AddScheduledMarkerChunkAsync();
            using AesGcmStreamCipher cipher = CreateCipher();
            var job = CreateJob(storage, cipher);
            string tempMarkerPath = job.GetCompletionMarkerPathForTests();
            DeleteTempMarker(tempMarkerPath);
            Directory.CreateDirectory(Path.GetDirectoryName(tempMarkerPath)!);
            await File.WriteAllTextAsync(tempMarkerPath, "test marker");

            try
            {
                await job.RunOnceAsync();
                DbContext.ChangeTracker.Clear();

                Chunk markerChunk = (await DbContext.Chunks.FindAsync(Ctn2RewriteJob.CompletionStorageMarkerHash))!;
                bool storageMarkerExists = await storage.ExistsAsync(Ctn2RewriteJob.CompletionStorageMarkerKey);

                Assert.Multiple(() =>
                {
                    Assert.That(storageMarkerExists, Is.True);
                    Assert.That(File.Exists(tempMarkerPath), Is.True);
                    Assert.That(markerChunk.GCScheduledAfter, Is.Null);
                });
            }
            finally
            {
                DeleteTempMarker(tempMarkerPath);
            }
        }

        private static async Task WriteStorageMarkerAsync(InMemoryStorage storage)
        {
            await using var stream = new MemoryStream([1, 2, 3], writable: false);
            await storage.WriteAsync(Ctn2RewriteJob.CompletionStorageMarkerKey, stream);
        }

        private async Task AddScheduledMarkerChunkAsync()
        {
            DbContext.Chunks.Add(new Chunk
            {
                Hash = Ctn2RewriteJob.CompletionStorageMarkerHash,
                PlainSizeBytes = 3,
                StoredSizeBytes = 3,
                CompressionAlgorithm = CompressionProcessor.Algorithm,
                GCScheduledAfter = DateTime.UtcNow.AddMinutes(-1)
            });
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        private static AesGcmStreamCipher CreateCipher()
        {
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            return MasterKeySentinelStore.CreateCipher(settings);
        }

        private Ctn2RewriteJob CreateJob(InMemoryStorage storage, IStreamCipher cipher)
        {
            return new Ctn2RewriteJob(
                storage,
                new ThrowingBackendProvider(),
                DbContext,
                cipher,
                NullLogger<Ctn2RewriteJob>.Instance);
        }

        private static void DeleteTempMarker(string tempMarkerPath)
        {
            if (File.Exists(tempMarkerPath))
            {
                File.Delete(tempMarkerPath);
            }
        }

        private class ThrowingBackendProvider : IStorageBackendProvider
        {
            public IStorageBackend GetBackend()
            {
                throw new InvalidOperationException("CTN2 rewrite should not touch backend after a completion marker is found.");
            }
        }
    }
}
