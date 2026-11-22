// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Common;
using Cotton.TextExtraction;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class DocumentTextExtractionTests
    {
        [Test]
        public async Task Word_ExtractsBodyHeadersAndFooters()
        {
            using MemoryStream stream = new(WordTestDocument.Create());
            WordDocumentTextExtractor extractor = new(NullLogger<WordDocumentTextExtractor>.Instance);

            TextExtractionResult result = await extractor.ExtractAsync(stream);

            Assert.Multiple(() =>
            {
                Assert.That(result.Text, Does.Contain("Body text").And.Contain("Header text").And.Contain("Footer text"));
                Assert.That(result.IsTruncated, Is.False);
                Assert.That(stream.CanRead, Is.True);
            });
        }

        [Test]
        public async Task Word_MissingHeader_IsReportedAsUnreadableContent()
        {
            using MemoryStream stream = new(WordTestDocument.Create(missingHeader: true));
            WordDocumentTextExtractor extractor = new(NullLogger<WordDocumentTextExtractor>.Instance);

            FileTextExtractionException? error = await Assert.ThrowsAsync<FileTextExtractionException>(
                async () => await extractor.ExtractAsync(stream));

            Assert.That(error?.InnerException, Is.TypeOf<InvalidDataException>());
        }
    }
}
