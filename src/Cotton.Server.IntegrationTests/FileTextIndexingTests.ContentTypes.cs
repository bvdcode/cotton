// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Helpers;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Services.Computation;
using Cotton.TextExtraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public partial class FileTextIndexingTests
    {
        [Test]
        public async Task Index_TriesEachRelatedTypeOnce_AndClearsEarlierExtractionError()
        {
            FileManifest manifest = await AddFileAsync("opaque-name", "application/octet-stream", [1, 2, 3]);
            foreach (string name in new[] { "first.pdf", "second.PDF", "third.png", "fourth.PNG" })
            {
                AddReference(manifest, name, _folder);
            }
            await _db.SaveChangesAsync();
            Guid id = manifest.Id;
            _db.ChangeTracker.Clear();
            RecordingFileTextExtractor extractor = new(["application/pdf", "image/png"], (source, attempt) =>
            {
                Assert.That(source.Position, Is.Zero);
                if (attempt == 1)
                {
                    source.Position = 1;
                    throw new FileTextExtractionException("Unsupported content for this type.", new InvalidDataException());
                }
                return "Extracted document text";
            });
            IndexFileTextRequestHandler handler = new(_db, _storage, new FileTextExtractorProvider([extractor]),
                _services.GetRequiredService<ComputationService>(), _services.GetRequiredService<IOptions<TextIndexingOptions>>(),
                NullLogger<IndexFileTextRequestHandler>.Instance);

            await handler.Handle(new IndexFileTextRequest(id), CancellationToken.None);
            _db.ChangeTracker.Clear();

            Assert.That(extractor.Attempts, Is.EqualTo(2));
            Assert.That(await _db.FileEmbeddings.AnyAsync(), Is.True);
            Assert.That((await _db.FileManifests.SingleAsync()).TextIndexError, Is.Null);
        }
    }
}
