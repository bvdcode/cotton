// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Services.Search;
using EasyExtensions.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public partial class FileTextIndexingTests
    {
        [TestCase("csv")]
        [TestCase("tsv")]
        [TestCase("json")]
        [TestCase("xml")]
        [TestCase("css")]
        [TestCase("js")]
        public async Task Index_OversizedStructuredFile_SkipsStorageAndWorker_AndIsNotRetried(string extension)
        {
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxStructuredFileBytes = 8;
            FileManifest manifest = await AddFileAsync($"dataset.{extension}", "application/octet-stream", "123456789"u8.ToArray());
            Guid id = manifest.Id;
            await foreach (string key in _storage.ListAllKeysAsync())
            {
                await _storage.DeleteAsync(key);
            }
            _db.ChangeTracker.Clear();

            await IndexAsync(id);
            _db.ChangeTracker.Clear();
            _failure.Enabled = true;
            await IndexAsync(id);
            _failure.Enabled = false;

            FileManifest stored = await _db.FileManifests.SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(stored.TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
                Assert.That(stored.TextIndexError, Does.StartWith("file_too_large:"));
                Assert.That(_worker.Addresses, Is.Empty);
            });
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);
        }

        [Test]
        public async Task Batch_SkipsOversizedCsv_AndIndexesFileAtLimit()
        {
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxStructuredFileBytes = 8;
            FileManifest large = await AddFileAsync("large.csv", "text/csv", "123456789"u8.ToArray());
            FileManifest small = await AddFileAsync("small.csv", "text/csv", "item,cat"u8.ToArray());
            _db.ChangeTracker.Clear();

            await _services.GetRequiredService<IMediator>()
                .Send(new IndexFileTextRequest([large.Id, small.Id]), CancellationToken.None);
            _db.ChangeTracker.Clear();

            Assert.That(string.Concat(_worker.Batches.SelectMany(batch => batch)), Is.EqualTo("item,cat"));
            Assert.That((await _db.FileEmbeddings.SingleAsync()).FileManifestId, Is.EqualTo(small.Id));
            Assert.That(await _db.FileManifests.AllAsync(file => file.TextIndexVersion == VectorIndexDefinition.Version), Is.True);
            Assert.That((await _db.FileManifests.SingleAsync(file => file.Id == small.Id)).TextIndexError, Is.Null);
        }

        [Test]
        public async Task Index_SizeLimit_DoesNotRemoveAlreadyIndexedVectors()
        {
            FileManifest manifest = await AddFileAsync("indexed.csv", "text/csv", "item,description"u8.ToArray());
            await IndexAsync(manifest.Id);
            _db.ChangeTracker.Clear();
            Guid[] previousIds = await _db.FileEmbeddings.Select(vector => vector.Id).ToArrayAsync();
            int calls = _worker.EmbedCalls;
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxStructuredFileBytes = 8;

            await IndexAsync(manifest.Id);

            Assert.That(await _db.FileEmbeddings.Select(vector => vector.Id).ToArrayAsync(), Is.EquivalentTo(previousIds));
            Assert.That(_worker.EmbedCalls, Is.EqualTo(calls));
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexError, Is.Null);
        }

        [TestCase("txt")]
        [TestCase("md")]
        [TestCase("ts")]
        [TestCase("yaml")]
        public async Task Index_PlainText_IndexesOnlyTheConfiguredPrefix(string extension)
        {
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxExtractedTextBytes = 7;
            FileManifest manifest = await AddFileAsync($"document.{extension}", "text/plain", Encoding.UTF8.GetBytes("aБ😀中 tail"));
            _db.ChangeTracker.Clear();

            await IndexAsync(manifest.Id);
            _db.ChangeTracker.Clear();

            Assert.That(string.Concat(_worker.Batches.SelectMany(batch => batch)), Is.EqualTo("aБ😀"));
            FileManifest indexed = await _db.FileManifests.SingleAsync();
            Assert.That(indexed.TextIndexError, Is.EqualTo("text_truncated: indexed the beginning within 7 UTF-8 bytes."));
            Assert.That(indexed.TextIndexVersion, Is.EqualTo(VectorIndexDefinition.Version));
        }

        [Test]
        public async Task Index_Pdf_LimitsExtractedTextInsteadOfSourceFileSize()
        {
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxStructuredFileBytes = 8;
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxExtractedTextBytes = 32;
            FileManifest manifest = await AddFileAsync("document.pdf", "application/pdf", PdfTestDocument.Create("Document text"));
            Assert.That(manifest.SizeBytes, Is.GreaterThan(32));
            _db.ChangeTracker.Clear();

            await IndexAsync(manifest.Id);
            _db.ChangeTracker.Clear();

            Assert.That(string.Concat(_worker.Batches.SelectMany(batch => batch)), Does.Contain("Document text"));
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexError, Is.Null);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.True);
        }

        [Test]
        public async Task Index_Html_IgnoresMarkupAndScriptsWhenApplyingTextBudget()
        {
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxStructuredFileBytes = 8;
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxExtractedTextBytes = 32;
            FileManifest manifest = await AddFileAsync("messages.html", "text/html",
                Encoding.UTF8.GetBytes("<html><body><script>ignored content</script><style>ignored styles</style><p>Chat message</p></body></html>"));
            Assert.That(manifest.SizeBytes, Is.GreaterThan(32));
            _db.ChangeTracker.Clear();

            await IndexAsync(manifest.Id);
            _db.ChangeTracker.Clear();

            Assert.That(string.Concat(_worker.Batches.SelectMany(batch => batch)), Is.EqualTo("Chat message"));
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexError, Is.Null);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.True);
        }
    }
}
