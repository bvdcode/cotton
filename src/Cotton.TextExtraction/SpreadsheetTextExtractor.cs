// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.Documents.Spreadsheets;
using Mediana.Documents.Xlsx;
using Microsoft.Extensions.Logging;

namespace Cotton.TextExtraction
{
    public class SpreadsheetTextExtractor(ILogger<SpreadsheetTextExtractor> logger) : DocumentTextExtractor(logger)
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private static readonly XlsxReaderOptions ReaderOptions = new()
        {
            MaximumSharedStrings = 500_000,
            MaximumStringCharacters = 8 * 1024 * 1024,
        };

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ReadTextAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            byte[] input = await ReadDocumentAsync(source, cancellationToken);
            using XlsxWorkbook workbook = XlsxReader.Open(input, ReaderOptions, cancellationToken);
            foreach (SpreadsheetSheet sheet in workbook.Sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sheet.Kind != SpreadsheetSheetKind.Worksheet)
                {
                    continue;
                }
                text.AppendNormalized(sheet.Name);
                text.AppendLineBreak();
                if (text.IsTruncated)
                {
                    return;
                }
                foreach (SpreadsheetRow row in workbook.ReadRows(sheet.Index, cancellationToken))
                {
                    foreach (SpreadsheetCell cell in row.Cells)
                    {
                        text.AppendNormalized(cell.Value.ToString());
                        if (text.IsTruncated)
                        {
                            return;
                        }
                        text.AppendNormalized("\t");
                    }
                    text.AppendLineBreak();
                }
            }
        }
    }
}
