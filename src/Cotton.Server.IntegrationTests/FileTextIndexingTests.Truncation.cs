// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Handlers.Files;
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
        [Test]
        public async Task Index_TruncatedText_StoresPrefixAndMarkerOnce_WithIndependentBudgetPerFile()
        {
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxExtractedTextBytes = 7;
            FileManifest first = await AddFileAsync("first.txt", "text/plain", Encoding.UTF8.GetBytes("aБ😀中 tail"));
            FileManifest second = await AddFileAsync("second.txt", "text/plain", Encoding.UTF8.GetBytes("123456789"));
            Guid[] ids = [first.Id, second.Id];
            _db.ChangeTracker.Clear();

            await _services.GetRequiredService<IMediator>().Send(new IndexFileTextRequest(ids), CancellationToken.None);
            _db.ChangeTracker.Clear();
            List<FileManifest> indexed = await _db.FileManifests.ToListAsync();
            List<FileEmbedding> vectors = await _db.FileEmbeddings.ToListAsync();

            Assert.That(string.Concat(_worker.Batches.SelectMany(batch => batch)), Is.EqualTo("aБ😀1234567"));
            Assert.That(indexed.All(file => file.TextIndexVersion == VectorIndexDefinition.Version), Is.True);
            Assert.That(indexed.All(file => file.TextIndexError == "text_truncated: indexed the beginning within 7 UTF-8 bytes."), Is.True);
            Assert.That(vectors.Select(vector => vector.FileManifestId), Is.EquivalentTo(ids));
            int calls = _worker.EmbedCalls;
            await _services.GetRequiredService<IMediator>().Send(new IndexFileTextRequest(ids), CancellationToken.None);
            Assert.That(_worker.EmbedCalls, Is.EqualTo(calls));
        }

        [Test]
        public async Task Index_TruncationMarker_RollsBackWithVectorsOnSaveFailure()
        {
            _services.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxExtractedTextBytes = 5;
            FileManifest manifest = await AddFileAsync("retry.txt", "text/plain", Encoding.UTF8.GetBytes("Hello world"));
            Guid id = manifest.Id;
            _db.ChangeTracker.Clear();
            _failure.Enabled = true;

            Assert.ThrowsAsync<DbUpdateException>(() => IndexAsync(id));
            _failure.Enabled = false;
            _db.ChangeTracker.Clear();
            FileManifest pending = await _db.FileManifests.SingleAsync();
            Assert.That(pending.TextIndexError, Is.Null);
            Assert.That(pending.TextIndexVersion, Is.Zero);
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.False);

            await IndexAsync(id);
            _db.ChangeTracker.Clear();
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexError, Does.StartWith("text_truncated:"));
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.True);
        }
    }
}
