// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Server.IntegrationTests
{
    public class SpreadsheetChartSheetTests
    {
        [Test]
        public async Task Extract_ReadsWorksheetsBeforeAndAfterChartSheet()
        {
            using MemoryStream source = new(SpreadsheetTestDocument.CreateWithChartSheet());
            SpreadsheetTextExtractor extractor = new(NullLogger<SpreadsheetTextExtractor>.Instance);

            TextExtractionResult result = await extractor.ExtractAsync(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(new TextExtractionResult(
                    string.Join(Environment.NewLine, "Before", "First worksheet", "After", "Last worksheet"), false)));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task Extract_ChartSheetWithoutWorksheets_ReturnsEmptyText()
        {
            using MemoryStream source = new(SpreadsheetTestDocument.CreateWithChartSheet(includeWorksheets: false));
            SpreadsheetTextExtractor extractor = new(NullLogger<SpreadsheetTextExtractor>.Instance);

            Assert.That(await extractor.ExtractAsync(source), Is.EqualTo(new TextExtractionResult(string.Empty, false)));
        }
    }
}
