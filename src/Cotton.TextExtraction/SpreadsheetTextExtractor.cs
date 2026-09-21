// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using System.IO.Packaging;

namespace Cotton.TextExtraction
{
    public class SpreadsheetTextExtractor(ILogger<SpreadsheetTextExtractor> logger) : IFileTextExtractor
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public IEnumerable<string> SupportedContentTypes => [ContentType];

        public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using SeekableReadStream seekable = await SeekableReadStream.OpenAsync(source, cancellationToken);
                using SpreadsheetDocument document = SpreadsheetDocument.Open(seekable.Stream, false);
                WorkbookPart workbookPart = document.WorkbookPart
                    ?? throw new FileFormatException("The workbook has no main part.");
                Workbook workbook = workbookPart.Workbook
                    ?? throw new FileFormatException("The workbook root is missing.");
                SharedStringItem[] sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?
                    .Elements<SharedStringItem>().ToArray() ?? [];
                List<string> lines = [];
                foreach (Sheet sheet in workbook.Sheets?.Elements<Sheet>() ?? [])
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relationshipId = sheet.Id?.Value
                        ?? throw new FileFormatException("A worksheet has no relationship id.");
                    WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(relationshipId);
                    lines.Add(sheet.Name?.Value ?? string.Empty);
                    Worksheet worksheet = worksheetPart.Worksheet
                        ?? throw new FileFormatException("A worksheet is empty.");
                    foreach (Row row in worksheet.Descendants<Row>())
                    {
                        string[] values = [.. row.Elements<Cell>().Select(cell => GetCellText(cell, sharedStrings))];
                        lines.Add(string.Join('\t', values));
                    }
                }
                return TextExtractionUtilities.JoinLines(lines);
            }
            catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException)
            {
                logger.LogWarning(ex, "Unable to extract spreadsheet text.");
                throw new FileTextExtractionException("Unable to read spreadsheet text.", ex);
            }
        }

        private static string GetCellText(Cell cell, IReadOnlyList<SharedStringItem> sharedStrings)
        {
            if (cell.DataType?.Value == CellValues.SharedString
                && int.TryParse(cell.CellValue?.Text, out int sharedStringIndex)
                && sharedStringIndex >= 0
                && sharedStringIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedStringIndex].InnerText;
            }
            if (cell.DataType?.Value == CellValues.InlineString)
            {
                return cell.InlineString?.InnerText ?? string.Empty;
            }
            if (cell.DataType?.Value == CellValues.Boolean)
            {
                return cell.CellValue?.Text == "1" ? bool.TrueString : bool.FalseString;
            }
            return cell.CellValue?.Text ?? string.Empty;
        }
    }
}
