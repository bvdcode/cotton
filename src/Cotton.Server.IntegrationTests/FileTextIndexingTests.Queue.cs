// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services.Search;
using Cotton.TextExtraction;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public partial class FileTextIndexingTests
    {
        [Test]
        public async Task Queue_BatchesSharedManifestsOnce_AndStopsAfterAllAreIndexed()
        {
            List<Guid> expected = [];
            for (int index = 0; index < 33; index++)
            {
                FileManifest manifest = await AddFileAsync($"document-{index}.pdf", PdfTextExtractor.ContentType,
                    System.Text.Encoding.UTF8.GetBytes($"Document {index}"));
                AddReference(manifest, $"copy-{index}.pdf", _folder);
                expected.Add(manifest.Id);
            }
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
            List<Guid> processed = [];
            foreach (int expectedBatchSize in new[] { 32, 1, 0 })
            {
                List<FileManifest> batch = await FileTextIndexQuery.Pending(_db, [PdfTextExtractor.ContentType])
                    .OrderBy(manifest => manifest.CreatedAt).ThenBy(manifest => manifest.Id)
                    .Take(32).ToListAsync();
                Assert.That(batch.Count, Is.EqualTo(expectedBatchSize));
                foreach (FileManifest manifest in batch)
                {
                    processed.Add(manifest.Id);
                    manifest.TextIndexVersion = VectorIndexDefinition.Version;
                }
                await _db.SaveChangesAsync();
                _db.ChangeTracker.Clear();
            }

            Assert.That(processed, Is.EquivalentTo(expected));
            Assert.That(processed.Distinct().Count(), Is.EqualTo(expected.Count));
        }
    }
}
