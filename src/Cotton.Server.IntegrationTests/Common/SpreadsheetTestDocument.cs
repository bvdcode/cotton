// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Cotton.Server.IntegrationTests.Common
{
    internal static class SpreadsheetTestDocument
    {
        public static byte[] CreateWithChartSheet(bool includeWorksheets = true)
        {
            using MemoryStream stream = new();
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbook = document.AddWorkbookPart();
                Sheets sheets = new();
                workbook.Workbook = new Workbook(sheets);
                if (includeWorksheets)
                {
                    AppendWorksheet("Before", "First worksheet");
                }
                ChartsheetPart chart = workbook.AddNewPart<ChartsheetPart>();
                chart.Chartsheet = new Chartsheet(new SheetViews(new SheetView { WorkbookViewId = 0 }));
                sheets.Append(new Sheet
                {
                    Id = workbook.GetIdOfPart(chart),
                    SheetId = (uint)sheets.ChildElements.Count + 1,
                    Name = "Chart",
                });
                if (includeWorksheets)
                {
                    AppendWorksheet("After", "Last worksheet");
                }

                void AppendWorksheet(string name, string value)
                {
                    WorksheetPart worksheet = workbook.AddNewPart<WorksheetPart>();
                    worksheet.Worksheet = new Worksheet(new SheetData(new Row(new Cell
                    {
                        DataType = CellValues.String,
                        CellValue = new CellValue(value),
                    })));
                    sheets.Append(new Sheet
                    {
                        Id = workbook.GetIdOfPart(worksheet),
                        SheetId = (uint)sheets.ChildElements.Count + 1,
                        Name = name,
                    });
                }
            }
            return stream.ToArray();
        }
    }
}
