// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text;
using EasyExtensions.Drawing.Helpers;
using ExcelDataReader;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
        private static readonly FontFamily FontFamily = FontHelpers.GetAnyFont().Family;

        static SpreadsheetPreviewGenerator()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public string Id => "spreadsheet";
        public int Version => 0;
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
            using IExcelDataReader reader = ExcelReaderFactory.CreateReader(input, new ExcelReaderConfiguration
            {
                LeaveOpen = true,
                SinglePassMode = true,
            });
            List<string[]> rows = [];
            int scanned = 0;
            int firstRow = 1;
            while (rows.Count < MaximumRows && scanned++ < MaximumScanRows && reader.Read())
            {
                string[] values = Enumerable.Range(0, Math.Min(MaximumColumns, reader.FieldCount))
                    .Select(column => Convert.ToString(reader.GetValue(column), CultureInfo.InvariantCulture) ?? string.Empty)
                    .ToArray();
                if (rows.Count > 0 || values.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    if (rows.Count == 0)
                    {
                        firstRow = scanned;
                    }
                    rows.Add(values);
                }
            }

            using Image<Rgba32> image = new(Width, Height, Color.White.ToPixel<Rgba32>());
            Font font = FontFamily.CreateFont(15);
            image.Mutate(context => context.Paint(canvas =>
            {
                canvas.Fill(Brushes.Solid(Color.WhiteSmoke), new RectanglePolygon(0, 0, Width, TopMargin));
                canvas.DrawText(new RichTextOptions(font) { Origin = new PointF(LeftMargin, 10) },
                    reader.Name, Brushes.Solid(Color.Black), null);
                for (int column = 0; column < MaximumColumns; column++)
                {
                    canvas.DrawText(new RichTextOptions(font)
                    { Origin = new PointF(LeftMargin + column * ColumnWidth + 4, TopMargin - RowHeight) },
                        ((char)('A' + column)).ToString(), Brushes.Solid(Color.DimGray), null);
                    canvas.Fill(Brushes.Solid(Color.LightGray),
                        new RectanglePolygon(LeftMargin + column * ColumnWidth, TopMargin - RowHeight, 1, Height));
                }
                for (int row = 0; row < MaximumRows; row++)
                {
                    int top = TopMargin + row * RowHeight;
                    canvas.Fill(Brushes.Solid(Color.LightGray), new RectanglePolygon(8, top, Width - 16, 1));
                    canvas.DrawText(new RichTextOptions(font) { Origin = new PointF(12, top + 5) },
                        (firstRow + row).ToString(CultureInfo.InvariantCulture), Brushes.Solid(Color.DimGray), null);
                    if (row >= rows.Count)
                    {
                        continue;
                    }
                    for (int column = 0; column < rows[row].Length; column++)
                    {
                        canvas.DrawText(new RichTextOptions(font)
                        { Origin = new PointF(LeftMargin + column * ColumnWidth + 4, top + 5) },
                            FitText(rows[row][column], font), Brushes.Solid(Color.Black), null);
                    }
                }
            }));
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size).ConfigureAwait(false);
        }

        private static string FitText(string value, Font font)
        {
            string text = value.ReplaceLineEndings(" ");
            if (text.Length > 64)
            {
                text = text[..64];
            }
            TextOptions options = new(font);
            while (text.Length > 0 && TextMeasurer.MeasureBounds(text, options).Width > ColumnWidth - 8)
            {
                text = text[..^1];
            }
            return text;
        }
    }
}
