// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Server;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Jobs;
using Cotton.Server.Services.Search;
using Cotton.TextExtraction;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using EasyExtensions.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public partial class FileTextIndexingTests
    {
        [Test]
        public async Task Recovery_EmptyTable_ResetsAllBatchesAndPreservesPreviewState()
        {
            List<FileManifest> manifests = Enumerable.Range(0, 1001).Select(index => new FileManifest
            {
                ContentType = string.Empty,
                ProposedContentHash = BitConverter.GetBytes(index),
                TextIndexVersion = index % 2,
                TextIndexError = "Previous indexing error",
                PreviewGenerationError = "Previous preview error",
                PreviewGeneratorVersion = 7,
                SizeBytes = index,
            }).ToList();
            _db.FileManifests.AddRange(manifests);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            await RecoverAsync();

            List<FileManifest> recovered = await _db.FileManifests.AsNoTracking().ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(recovered.Count, Is.EqualTo(manifests.Count));
                Assert.That(recovered.All(file => file.TextIndexVersion == 0 && file.TextIndexError is null), Is.True);
                Assert.That(recovered.All(file => file.PreviewGeneratorVersion == 7
                    && file.PreviewGenerationError == "Previous preview error"), Is.True);
                Assert.That(recovered.Select(file => file.SizeBytes), Is.EquivalentTo(manifests.Select(file => file.SizeBytes)));
            });
        }

        [Test]
        public async Task Recovery_PopulatedTable_PreservesIndexState()
        {
            FileManifest manifest = await AddFileAsync("indexed.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Indexed"));
            manifest.TextIndexVersion = VectorIndexDefinition.Version;
            manifest.TextIndexError = "Existing state";
            _db.FileEmbeddings.Add(new FileEmbedding { FileManifestId = manifest.Id, Embedding = [1] });
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            await RecoverAsync();

            FileManifest stored = await _db.FileManifests.SingleAsync();
            Assert.That(stored.TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
            Assert.That(stored.TextIndexError, Is.EqualTo("Existing state"));
            Assert.That(await _db.FileEmbeddings.CountAsync(), Is.EqualTo(1));
        }

        [Test]
        public async Task Recovery_SaveFailure_RetriesOnNextInvocation()
        {
            FileManifest manifest = await AddFileAsync("retry.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Retry"));
            manifest.TextIndexVersion = VectorIndexDefinition.Version;
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
            _failure.Enabled = true;

            Assert.ThrowsAsync<DbUpdateException>(RecoverAsync);

            _failure.Enabled = false;
            Assert.That((await _db.FileManifests.AsNoTracking().SingleAsync()).TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
            await RecoverAsync();
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexVersion, Is.Zero);
        }

        [Test]
        public async Task Job_EmptyEmbeddingTable_ReindexesProcessedManifest()
        {
            await PrepareVectorIndexAsync();
            FileManifest manifest = await AddFileAsync("restored.pdf", PdfTextExtractor.ContentType, PdfTestDocument.Create("Restored document"));
            manifest.TextIndexVersion = VectorIndexDefinition.Version;
            manifest.TextIndexError = "Old error";
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            await _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);

            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.True);
            FileManifest indexed = await _db.FileManifests.SingleAsync();
            Assert.That(indexed.TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
            Assert.That(indexed.TextIndexError, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Job_OnlyEmptyOrInvalidFiles_DoesNotResetAgain(bool invalid)
        {
            await PrepareVectorIndexAsync();
            byte[] bytes = invalid ? Encoding.UTF8.GetBytes("not PDF") : PdfTestDocument.Create("");
            await AddFileAsync("empty.pdf", PdfTextExtractor.ContentType, bytes);
            _db.ChangeTracker.Clear();
            await _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
            int calls = _worker.EmbedCalls;
            _failure.Enabled = true;

            await _services.GetRequiredService<GenerateFileEmbeddingsJob>().Execute(null!);

            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
            Assert.That(_worker.EmbedCalls, Is.EqualTo(calls));
        }

        private Task RecoverAsync() => _services.GetRequiredService<IMediator>()
            .Send(new RecoverFileTextIndexRequest(), CancellationToken.None);

        private async Task PrepareVectorIndexAsync()
        {
            if (!await _db.Database.IsExtensionAvailableAsync("vector"))
            {
                Assert.Ignore("The PostgreSQL test server does not have the pgvector package.");
            }
            await _db.Database.EnsurePostgresExtensionAsync("vector", CancellationToken.None);
            await _services.GetRequiredService<IMediator>().Send(new BuildVectorIndexRequest(), CancellationToken.None);
        }
    }
}
