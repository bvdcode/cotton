// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Numerics;
using Mediana.Documents.Spreadsheets;
using Mediana.Documents.Xls;
using Mediana.Documents.Xlsx;
using Mediana.Drawing;
using SixLabors.ImageSharp;
using ImagePixel = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Cotton.Previews
{
    public class SpreadsheetPreviewGenerator : IPreviewGenerator
    {
        private const long MaximumSourceBytes = 64 * 1024 * 1024;
        private const int MaximumColumns = 6;
        private const int MaximumRows = 14;
        private const int MaximumScanRows = 1024;
        private const int Width = 640;
        private const int Height = 480;
        private const int RowHeight = 28;
        private const int ColumnWidth = 96;
        private const int LeftMargin = 48;
        private const int TopMargin = 60;

        public string Id => "spreadsheet";
        public int Version => 2;
        public int Priority => 0;
        public IEnumerable<string> SupportedContentTypes =>
        [
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-excel",
        ];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
            using MemoryStream input = new();
            await PreviewStream.CopyBoundedAsync(stream, input, MaximumSourceBytes).ConfigureAwait(false);
            using SpreadsheetWorkbook workbook = OpenWorkbook(input.GetBuffer().AsMemory(0, (int)input.Length));
            SpreadsheetSheet sheet = workbook.Sheets.FirstOrDefault(sheet => sheet.Kind == SpreadsheetSheetKind.Worksheet)
                ?? throw new InvalidDataException("The workbook has no worksheet.");
            List<string[]> rows = [];
            int firstRow = 1;
            foreach (SpreadsheetRow row in workbook.ReadRows(sheet.Index))
            {
                if (row.RowIndex >= MaximumScanRows)
                {
                    break;
                }
                string[] values = new string[MaximumColumns];
                Array.Fill(values, string.Empty);
                foreach (SpreadsheetCell cell in row.Cells)
                {
                    if (cell.ColumnIndex < MaximumColumns)
                    {
                        values[cell.ColumnIndex] = cell.Value.ToString();
                    }
                }
                if (rows.Count == 0)
                {
                    if (values.All(string.IsNullOrWhiteSpace))
                    {
                        continue;
                    }
                    firstRow = row.RowIndex + 1;
                }
                int displayRow = row.RowIndex - firstRow + 1;
                while (rows.Count < displayRow && rows.Count < MaximumRows)
                {
                    rows.Add([]);
                }
                if (rows.Count >= MaximumRows)
                {
                    break;
                }
                rows.Add(values);
                if (rows.Count == MaximumRows)
                {
                    break;
                }
            }

            using PixelBuffer pixels = new(Width, Height);
            pixels.Clear(new Rgba32(255, 255, 255, 255));
            RasterCanvas canvas = new(pixels);
            PreviewTextRenderer font = new(15);
            Rgba32 black = new(0, 0, 0, 255);
            Rgba32 gray = new(105, 105, 105, 255);
            Rgba32 lightGray = new(211, 211, 211, 255);
            Fill(canvas, 0, 0, Width, TopMargin, new Rgba32(245, 245, 245, 255));
            font.Draw(canvas, sheet.Name, LeftMargin, 10, black);
            for (int column = 0; column < MaximumColumns; column++)
            {
                font.Draw(canvas, ((char)('A' + column)).ToString(),
                    LeftMargin + (column * ColumnWidth) + 4, TopMargin - RowHeight, gray);
                Fill(canvas, LeftMargin + (column * ColumnWidth), TopMargin - RowHeight, 1, Height, lightGray);
            }
            for (int row = 0; row < MaximumRows; row++)
            {
                int top = TopMargin + (row * RowHeight);
                Fill(canvas, 8, top, Width - 16, 1, lightGray);
                font.Draw(canvas, (firstRow + row).ToString(CultureInfo.InvariantCulture), 12, top + 5, gray);
                if (row >= rows.Count)
                {
                    continue;
                }
                for (int column = 0; column < rows[row].Length; column++)
                {
                    font.Draw(canvas, FitText(rows[row][column], font),
                        LeftMargin + (column * ColumnWidth) + 4, top + 5, black);
                }
            }
            using Image<ImagePixel> image = Image.LoadPixelData<ImagePixel>(pixels.Pixels.Span, Width, Height);
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size).ConfigureAwait(false);
        }

        private static void Fill(RasterCanvas canvas, float x, float y, float width, float height, Rgba32 color)
        {
            canvas.FillPathAntialiased(new VectorPath().Rectangle(x, y, width, height), color, Matrix3x2.Identity);
        }

        private static SpreadsheetWorkbook OpenWorkbook(ReadOnlyMemory<byte> input)
        {
            ReadOnlySpan<byte> compoundFileSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
            if (input.Span.StartsWith(compoundFileSignature))
            {
                return XlsReader.Open(input);
            }
            if (input.Span.StartsWith("PK\x03\x04"u8))
            {
                return XlsxReader.Open(input);
            }
            throw new InvalidDataException("The source is not an XLS or XLSX workbook.");
        }

        private static string FitText(string value, PreviewTextRenderer font)
        {
            string text = value.ReplaceLineEndings(" ");
            if (text.Length > 64)
            {
                text = text[..64];
            }
            while (text.Length > 0 && font.MeasureWidth(text) > ColumnWidth - 8)
            {
                text = text[..^1];
            }
            return text;
        }
    }
}
