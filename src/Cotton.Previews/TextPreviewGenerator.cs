// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;
using Mediana.Drawing;
using SixLabors.ImageSharp;
using ImagePixel = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Cotton.Previews
{
    public class TextPreviewGenerator : IPreviewGenerator
    {
        public string Id => "text";

        public int Version => 2;

        public int Priority => 100;

        public IEnumerable<string> SupportedContentTypes =>
        [
            "text/plain",
            "text/markdown",
            "text/x-csharp",
            "text/css",
            "text/html",
            "text/javascript",
            "text/jscript",
            "text/xml",
            "text/csv",
            "application/xml",
            "application/json",
            "application/javascript",
            "application/x-sh",
        ];

        private const int MaxCharsToRead = 24_000;
        private const int MaxLinesToRender = 64;
        private const int MaxLineChars = 512;
        private const float PaddingRatio = 0.06f;
        private const float FontSizeRatio = 0.045f;

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            string text;
            using (StreamReader reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                char[] buffer = new char[Math.Min(MaxCharsToRead, 8192)];
                int total = 0;
                StringBuilder sb = new System.Text.StringBuilder();

                while (total < MaxCharsToRead)
                {
                    int want = Math.Min(buffer.Length, MaxCharsToRead - total);
                    int read = await reader.ReadAsync(buffer, 0, want).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    sb.Append(buffer, 0, read);
                    total += read;
                }

                text = sb.ToString();
            }

            string rawText = text;
            if (string.IsNullOrWhiteSpace(rawText))
            {
                rawText = "(empty file)";
            }

            int renderSize = Math.Max(size * 4, 512);
            float padding = renderSize * PaddingRatio;
            float paddingTop = padding * 1.3f;
            float fontSize = Math.Max(10f, renderSize * FontSizeRatio);
            PreviewTextRenderer font = new(fontSize);
            float wrapWidth = renderSize - (padding * 2);
            float maxHeight = renderSize - paddingTop - padding;
            float lineAdvance = fontSize * 1.25f;
            text = PrepareAndLayout(rawText, font, wrapWidth, maxHeight, lineAdvance);

            using PixelBuffer pixels = new(size, size);
            pixels.Clear(new Rgba32(255, 255, 255, 255));
            font.Draw(new RasterCanvas(pixels), text, padding, paddingTop,
                new Rgba32(0, 0, 0, 255), size / (float)renderSize);
            using Image<ImagePixel> output = Image.WrapMemory<ImagePixel>(pixels.Pixels, size, size);

            using MemoryStream ms = new MemoryStream();
            await output.SaveAsWebpAsync(ms, PreviewImageEncoder.Create(size)).ConfigureAwait(false);
            return ms.ToArray();
        }

        private static string PrepareAndLayout(
            string rawText,
            PreviewTextRenderer font,
            float wrapWidth,
            float maxHeight,
            float lineAdvance)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return string.Empty;
            }

            string normalized = NormalizeText(rawText);
            if (LooksBinary(rawText, normalized))
            {
                return string.Empty;
            }

            normalized = LimitLogicalLines(normalized, MaxLinesToRender);
            normalized = LimitLineWidth(normalized, MaxLineChars);
            return LayoutText(normalized, font, wrapWidth, maxHeight, lineAdvance);
        }

        private static bool LooksBinary(string raw, string normalized)
        {
            if (raw.Length == 0)
            {
                return false;
            }

            int dropped = raw.Length - normalized.Length;
            return dropped > (raw.Length / 4);
        }

        private static string LayoutText(
            string text,
            PreviewTextRenderer font,
            float wrapWidth,
            float maxHeight,
            float lineAdvance)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            float charWidth = Math.Max(1f, font.MeasureAdvance('M'));

            int cols = Math.Max(1, (int)Math.Floor(wrapWidth / charWidth));
            int rows = Math.Max(1, (int)Math.Floor(maxHeight / Math.Max(1f, lineAdvance)));
            string rawLines = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            StringBuilder sb = new System.Text.StringBuilder();
            int producedRows = 0;
            int i = 0;
            while (i < rawLines.Length && producedRows < rows)
            {
                int nextNl = rawLines.IndexOf('\n', i);
                string line = nextNl < 0 ? rawLines[i..] : rawLines[i..nextNl];

                int pos = 0;
                while (pos < line.Length && producedRows < rows)
                {
                    int take = Math.Min(cols, line.Length - pos);
                    while (take > 1 && font.MeasureWidth(line.Substring(pos, take)) > wrapWidth)
                    {
                        take--;
                    }
                    sb.Append(line.AsSpan(pos, take));
                    pos += take;
                    producedRows++;
                    if (producedRows < rows)
                    {
                        sb.Append('\n');
                    }
                }

                i = nextNl < 0 ? rawLines.Length : nextNl + 1;
                if (nextNl >= 0 && producedRows < rows && (sb.Length == 0 || sb[^1] != '\n'))
                {
                    sb.Append('\n');
                    producedRows++;
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string NormalizeText(string text)
        {
            Span<char> tmp = text.ToCharArray();
            int w = 0;

            for (int i = 0; i < tmp.Length; i++)
            {
                char c = tmp[i];
                if (c == '\0')
                {
                    continue;
                }

                if (c == '\r' || c == '\n' || c == '\t')
                {
                    tmp[w++] = c;
                    continue;
                }

                if (char.IsControl(c))
                {
                    continue;
                }

                tmp[w++] = c;
            }

            return new string(tmp[..w]);
        }

        private static string LimitLineWidth(string text, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            StringBuilder sb = new System.Text.StringBuilder(text.Length);
            int current = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    sb.Append(c);
                    current = 0;
                    continue;
                }

                if (current >= maxCharsPerLine)
                {
                    if (sb.Length > 0 && sb[^1] != '\n')
                    {
                        sb.Append("…\n");
                    }
                    current = 0;
                    continue;
                }

                sb.Append(c);
                current++;
            }

            return sb.ToString();
        }

        private static string LimitLogicalLines(string text, int maxLines)
        {
            int lines = 0;
            int idx = 0;

            while (idx < text.Length && lines < maxLines)
            {
                int next = text.IndexOf('\n', idx);
                if (next < 0)
                {
                    idx = text.Length;
                    break;
                }
                lines++;
                idx = next + 1;
            }

            if (idx >= text.Length)
            {
                return text;
            }

            return text[..idx].TrimEnd() + "\n…";
        }
    }
}
