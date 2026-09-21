// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.IntegrationTests.Common;
using Cotton.TextExtraction;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class PdfTextExtractionTests
    {
        private static PdfTextExtractor CreateExtractor() => new(NullLogger<PdfTextExtractor>.Instance);

        [Test]
        public async Task Extract_LimitIndexesBeginningOfFirstPage()
        {
            using MemoryStream source = new(PdfTestDocument.Create("First page", "Second page"));
            Assert.That(await CreateExtractor().ExtractAsync(source, 5),
                Is.EqualTo(new TextExtractionResult("First", true)));
        }

        [Test]
        public async Task Extract_ExactLimit_DoesNotCountGeneratedTrailingLineBreak()
        {
            using MemoryStream source = new(PdfTestDocument.Create("First"));
            Assert.That(await CreateExtractor().ExtractAsync(source, 5),
                Is.EqualTo(new TextExtractionResult("First", false)));
        }

        [Test]
        public async Task Extract_ReadsAllPagesInOrder_AndKeepsSourceOpen()
        {
            using MemoryStream source = new(PdfTestDocument.Create("First page", "Second page"));
            string text = (await CreateExtractor().ExtractAsync(source)).Text;
            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo($"First page{Environment.NewLine}Second page"));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task Extract_BlankPdf_ReturnsEmptyText()
        {
            using MemoryStream source = new(PdfTestDocument.Create(""));
            Assert.That((await CreateExtractor().ExtractAsync(source)).Text, Is.Empty);
        }

        [Test]
        public void Extract_InvalidPdf_ReportsUnreadableContent()
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes("not a PDF"));
            Assert.ThrowsAsync<FileTextExtractionException>(async () => await CreateExtractor().ExtractAsync(source));
        }

        [Test]
        public void Extract_Cancellation_IsPropagated()
        {
            using MemoryStream source = new(PdfTestDocument.Create("Cancelled"));
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await CreateExtractor().ExtractAsync(source, cancellationToken: cancellation.Token));
        }

        [Test]
        public void Provider_ResolvesMimeCaseInsensitively_AndRejectsUnsupportedTypes()
        {
            PdfTextExtractor extractor = CreateExtractor();
            FileTextExtractorProvider provider = new([extractor]);
            Assert.That(provider.GetExtractor("APPLICATION/PDF"), Is.SameAs(extractor));
            Assert.That(provider.GetExtractor("text/plain"), Is.Null);
        }
    }
}
