// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using System.IO.Packaging;

namespace Cotton.TextExtraction
{
    public class SpreadsheetTextExtractor(ILogger<SpreadsheetTextExtractor> logger) : FileTextExtractor
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
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
                foreach (Sheet sheet in workbook.Sheets?.Elements<Sheet>() ?? [])
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relationshipId = sheet.Id?.Value
                        ?? throw new FileFormatException("A worksheet has no relationship id.");
                    WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(relationshipId);
                    text.AppendNormalized(sheet.Name?.Value);
                    text.AppendLineBreak();
                    if (text.IsTruncated)
                    {
                        return;
                    }
                    Worksheet worksheet = worksheetPart.Worksheet
                        ?? throw new FileFormatException("A worksheet is empty.");
                    foreach (Row row in worksheet.Descendants<Row>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        foreach (Cell cell in row.Elements<Cell>())
                        {
                            text.AppendNormalized(GetCellText(cell, sharedStrings));
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
