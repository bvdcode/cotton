// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Server;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using Cotton.Server.Services.Search;
using Cotton.TextExtraction;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public partial class FileTextIndexingTests : IntegrationTestBase
    {
        public FileTextIndexingTests() : base($"cotton_text_index_tests_{Guid.NewGuid():N}")
        {
        }

        [TestCase("application/pdf", "document.bin")]
        [TestCase("application/octet-stream", "document.PDF")]
        public async Task Index_StoresAllFragmentsOnce_ForSharedManifest(string mime, string name)
        {
            FileManifest manifest = await AddFileAsync(name, mime, PdfTestDocument.Create("First document page", "Second document page"));
            AddReference(manifest, "copy.pdf", _folder);
            await _db.SaveChangesAsync();
            Guid id = manifest.Id;
            _db.ChangeTracker.Clear();

            await IndexAsync(id);
            _db.ChangeTracker.Clear();
            List<FileEmbedding> vectors = await _db.FileEmbeddings.OrderBy(vector => vector.FragmentIndex).ToListAsync();
            FileManifest indexed = await _db.FileManifests.SingleAsync();
            int calls = _worker.EmbedCalls;
            _db.ChangeTracker.Clear();
            await IndexAsync(id);

            Assert.Multiple(() =>
            {
                Assert.That(vectors.Count, Is.GreaterThan(1));
                Assert.That(vectors.Select(vector => vector.FragmentIndex), Is.EqualTo(Enumerable.Range(0, vectors.Count)));
                Assert.That(vectors.All(vector => vector.Embedding.Length == VectorIndexDefinition.Dimensions), Is.True);
                Assert.That(vectors.All(vector => vector.IndexVersion == VectorIndexDefinition.Version), Is.True);
                Assert.That(indexed.TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
                Assert.That(indexed.TextIndexError, Is.Null);
                Assert.That(_worker.EmbedCalls, Is.EqualTo(calls));
            });
            Assert.That(await _db.FileEmbeddings.CountAsync(), Is.EqualTo(vectors.Count));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Index_EmptyOrInvalidPdf_IsRecordedWithoutVectors_AndNotRetried(bool invalid)
        {
            byte[] bytes = invalid ? Encoding.UTF8.GetBytes("not PDF") : PdfTestDocument.Create("");
            FileManifest manifest = await AddFileAsync("empty.pdf", PdfTextExtractor.ContentType, bytes);
            Guid id = manifest.Id;
            _db.ChangeTracker.Clear();
            await IndexAsync(id);
            _db.ChangeTracker.Clear();
            await IndexAsync(id);
            FileManifest indexed = await _db.FileManifests.SingleAsync();
            Assert.That(indexed.TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
            Assert.That(indexed.TextIndexError is not null, Is.EqualTo(invalid));
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            Assert.That(_worker.EmbedCalls, Is.Zero);
        }

        [Test]
        public async Task Index_WorkerFailure_LeavesFilePending_AndCanBeRetried()
        {
            FileManifest manifest = await AddFileAsync("retry.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Retry document"));
            Guid id = manifest.Id;
            _db.ChangeTracker.Clear();
            _worker.StatusCode = HttpStatusCode.ServiceUnavailable;
            Assert.ThrowsAsync<HttpRequestException>(() => IndexAsync(id));
            _db.ChangeTracker.Clear();
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexVersion, Is.Zero);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            _worker.StatusCode = HttpStatusCode.OK;
            await IndexAsync(id);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.True);
        }

        [Test]
        public async Task Index_InvalidVectors_DoNotMarkFileProcessed()
        {
            FileManifest manifest = await AddFileAsync("invalid-vectors.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Invalid vectors"));
            _worker.Dimensions = 768;
            Assert.ThrowsAsync<ComputationException>(() => IndexAsync(manifest.Id));
            _db.ChangeTracker.Clear();
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexVersion, Is.Zero);
        }

        [Test]
        public async Task Index_SaveFailure_RollsBackVectorReplacementAndProcessingVersion()
        {
            FileManifest manifest = await AddFileAsync("atomic.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Atomic document"));
            FileEmbedding previous = new() { FileManifestId = manifest.Id, IndexVersion = 0, Embedding = [1] };
            _db.FileEmbeddings.Add(previous);
            await _db.SaveChangesAsync();
            Guid id = manifest.Id;
            Guid previousId = previous.Id;
            _db.ChangeTracker.Clear();
            _failure.Enabled = true;
            Assert.ThrowsAsync<DbUpdateException>(() => IndexAsync(id));
            _failure.Enabled = false;
            _db.ChangeTracker.Clear();
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexVersion, Is.Zero);
            Assert.That((await _db.FileEmbeddings.SingleAsync()).Id, Is.EqualTo(previousId));

            _db.ChangeTracker.Clear();
            await IndexAsync(id);
            Assert.That(await _db.FileEmbeddings.AnyAsync(vector => vector.Id == previousId), Is.False);
            Assert.That(await _db.FileEmbeddings.AllAsync(vector => vector.IndexVersion == VectorIndexDefinition.Version), Is.True);
        }

        [Test]
        public async Task Index_LaterBatchFailure_DoesNotPersistEarlierBatches()
        {
            FileManifest manifest = await AddFileAsync("batch.pdf", PdfTextExtractor.ContentType,
                PdfTestDocument.Create("This document needs several embedding batches, which must be stored together."));
            _worker.FailingEmbeddingCall = 2;
            Assert.ThrowsAsync<HttpRequestException>(() => IndexAsync(manifest.Id));
            _db.ChangeTracker.Clear();
            Assert.That(_worker.EmbedCalls, Is.EqualTo(2));
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexVersion, Is.Zero);
        }

        [Test]
        public async Task Index_CancellationDuringInference_DoesNotPersistPartialResult()
        {
            FileManifest manifest = await AddFileAsync("cancel.pdf", PdfTextExtractor.ContentType,
                PdfTestDocument.Create("This document needs several embedding batches and is cancelled during inference."));
            using CancellationTokenSource cancellation = new();
            _worker.EmbeddingRequested = () => cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(() => _services.GetRequiredService<IMediator>()
                .Send(new IndexFileTextRequest(manifest.Id), cancellation.Token));
            _db.ChangeTracker.Clear();
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexVersion, Is.Zero);
        }

        [Test]
        public async Task Queue_ExcludesHistoryTrashInactiveLayoutsAndUnsupportedFiles()
        {
            FileManifest current = await AddFileAsync("current.pdf", "application/octet-stream", PdfTestDocument.Create("Current"));
            FileManifest history = await AddFileAsync("history.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("History"));
            NodeFile historyFile = await _db.NodeFiles.SingleAsync(file => file.FileManifestId == history.Id);
            historyFile.OriginalNodeFileId = (await _db.NodeFiles.SingleAsync(file => file.FileManifestId == current.Id)).Id;
            Node trash = new() { Owner = _folder.Owner, Layout = _folder.Layout, Type = NodeType.Trash };
            trash.SetName("trash");
            await AddFileAsync("deleted.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Deleted"), trash);
            Node inactive = new() { Owner = _folder.Owner, Layout = new Layout { Owner = _folder.Owner }, Type = NodeType.Default };
            inactive.SetName("inactive");
            await AddFileAsync("inactive.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Inactive"), inactive);
            await AddFileAsync("unsupported.txt", "text/plain", Encoding.UTF8.GetBytes("plain text"));
            await AddFileAsync("misleading.txt", PdfTextExtractor.ContentType, PdfTestDocument.Create("Not named as PDF"));
            List<Guid> candidates = await FileTextIndexQuery.Pending(_db, [PdfTextExtractor.ContentType]).Select(file => file.Id).ToListAsync();
            Assert.That(candidates, Is.EqualTo(new[] { current.Id }));
        }

        [Test]
        public async Task Job_WithIndexingDisabled_DoesNotAccessDatabaseOrWorker()
        {
            await AddFileAsync("pending.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Pending document"));
            _settingsCache.InvalidateSettings(serverIsInitialized: true);
            _settingsCache.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(new CottonServerSettings
            {
                AllowGlobalIndexing = false,
                ComputionMode = ComputionMode.Remote,
                RemoteComputationRunnerUrl = "https://runner.example/",
            }));
            string? connectionString = _db.Database.GetConnectionString();
            _db.Database.SetConnectionString("Host=localhost;Port=1;Database=unused;Username=unused;Timeout=1");
            try
            {
                await _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);
            }
            finally
            {
                _db.Database.SetConnectionString(connectionString);
            }
            Assert.That(_worker.Addresses, Is.Empty);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexVersion, Is.Zero);
        }

        [Test]
        public async Task Job_WithoutExtension_DoesNotContactWorkerOrWriteVectors()
        {
            await AddFileAsync("pending.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Pending document"));
            await _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);
            Assert.That(_worker.Addresses, Is.Empty);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexVersion, Is.Zero);
        }

        [Test]
        public async Task Migration_AppliesWithoutVectorExtension_AndMatchesModel()
        {
            _db.ChangeTracker.Clear();
            await _db.Database.EnsureDeletedAsync();
            await _db.Database.MigrateAsync();
            FileManifest manifest = new() { ContentType = PdfTextExtractor.ContentType, ProposedContentHash = [1] };
            _db.FileManifests.Add(manifest);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
            FileManifest stored = await _db.FileManifests.SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(stored.TextIndexVersion, Is.Zero);
                Assert.That(stored.TextIndexError, Is.Null);
                Assert.That(_db.Database.HasPendingModelChanges(), Is.False);
            });
            Assert.That(await _db.Database.IsExtensionInstalledAsync("vector"), Is.False);
        }

        [Test]
        public async Task Job_WithPreparedDatabase_ProcessesPdfAndContinuesPastInvalidFiles()
        {
            if (!await _db.Database.IsExtensionAvailableAsync("vector"))
            {
                Assert.Ignore("The PostgreSQL test server does not have the pgvector package.");
            }
            await _db.Database.EnsurePostgresExtensionAsync("vector", CancellationToken.None);
            await _services.GetRequiredService<IMediator>().Send(new BuildVectorIndexRequest(), CancellationToken.None);
            await AddFileAsync("invalid.pdf", PdfTextExtractor.ContentType, Encoding.UTF8.GetBytes("bad PDF"));
            FileManifest valid = await AddFileAsync("valid.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Valid document"));
            Guid id = valid.Id;
            _db.ChangeTracker.Clear();

            await _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);

            Assert.That(await _db.FileManifests.AllAsync(file => file.TextIndexVersion == VectorIndexDefinition.Version), Is.True);
            Assert.That(await _db.FileEmbeddings.AnyAsync(vector => vector.FileManifestId == id), Is.True);
            int calls = _worker.EmbedCalls;
            await _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);
            Assert.That(_worker.EmbedCalls, Is.EqualTo(calls));
        }

        [Test]
        public async Task Job_WithPreparedDatabaseAndEmptyQueue_DoesNotContactWorker()
        {
            if (!await _db.Database.IsExtensionAvailableAsync("vector"))
            {
                Assert.Ignore("The PostgreSQL test server does not have the pgvector package.");
            }
            await _db.Database.EnsurePostgresExtensionAsync("vector", CancellationToken.None);
            await _services.GetRequiredService<IMediator>().Send(new BuildVectorIndexRequest(), CancellationToken.None);
            await AddFileAsync("unsupported.bin", "application/octet-stream", "Binary content"u8.ToArray());

            await _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);

            Assert.That(_worker.Addresses, Is.Empty);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
        }
    }
}
