// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using System.IO.Packaging;

namespace Cotton.TextExtraction
{
    public class SpreadsheetTextExtractor(ILogger<SpreadsheetTextExtractor> logger) : FileTextExtractor
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private const int MaxSharedStringCharacters = 8 * 1024 * 1024;
        private const int MaxSharedStrings = 500_000;

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
                List<string> sharedStrings = ReadSharedStrings(workbookPart.SharedStringTablePart, cancellationToken);
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
                    AppendWorksheet(worksheetPart, sharedStrings, text, cancellationToken);
                    if (text.IsTruncated)
                    {
                        return;
                    }
                }
            }
            catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException)
            {
                logger.LogWarning(ex, "Unable to extract spreadsheet text.");
                throw new FileTextExtractionException("Unable to read spreadsheet text.", ex);
            }
        }

        private static List<string> ReadSharedStrings(SharedStringTablePart? part, CancellationToken cancellationToken)
        {
            List<string> sharedStrings = [];
            if (part is null)
            {
                return sharedStrings;
            }
            int sharedStringCharacters = 0;
            using OpenXmlReader reader = OpenXmlReader.Create(part);
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!reader.IsStartElement || reader.ElementType != typeof(SharedStringItem))
                {
                    continue;
                }
                if (sharedStrings.Count >= MaxSharedStrings)
                {
                    throw new InvalidDataException("The spreadsheet shared string table is too large to index.");
                }
                string value = reader.LoadCurrentElement()?.InnerText
                    ?? throw new FileFormatException("A shared string is empty.");
                if (value.Length > MaxSharedStringCharacters - sharedStringCharacters)
                {
                    throw new InvalidDataException("The spreadsheet shared string table is too large to index.");
                }
                sharedStringCharacters += value.Length;
                sharedStrings.Add(value);
            }
            return sharedStrings;
        }

        private static void AppendWorksheet(
            WorksheetPart worksheetPart,
            IReadOnlyList<string> sharedStrings,
            TextExtractionBuffer text,
            CancellationToken cancellationToken)
        {
            using OpenXmlReader reader = OpenXmlReader.Create(worksheetPart);
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.IsStartElement && reader.ElementType == typeof(Cell))
                {
                    Cell cell = reader.LoadCurrentElement() as Cell
                        ?? throw new FileFormatException("A worksheet cell is empty.");
                    text.AppendNormalized(GetCellText(cell, sharedStrings));
                    if (text.IsTruncated)
                    {
                        return;
                    }
                    text.AppendNormalized("\t");
                }
                else if (reader.IsEndElement && reader.ElementType == typeof(Row))
                {
                    text.AppendLineBreak();
                }
            }
        }

        private static string GetCellText(Cell cell, IReadOnlyList<string> sharedStrings)
        {
            if (cell.DataType?.Value == CellValues.SharedString
                && int.TryParse(cell.CellValue?.Text, out int sharedStringIndex)
                && sharedStringIndex >= 0
                && sharedStringIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedStringIndex];
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
