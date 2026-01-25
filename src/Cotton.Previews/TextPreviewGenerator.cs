using EasyExtensions.Fonts.Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    public class TextPreviewGenerator : IPreviewGenerator
    {
        public IEnumerable<string> SupportedContentTypes =>
        [
            "text/plain",
            "text/markdown",
            "application/xml",
        ];

        private const int MaxCharsToRead = 24_000;
        private const int MaxLinesToRender = 64;
        private const int MaxLineChars = 512;
        private const float PaddingRatio = 0.06f;
        private const float FontSizeRatio = 0.045f;
        private const float LineSpacingRatio = 1.25f;

        private static readonly FontFamily _fontFamily = LoadFontFamily();

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = PreviewGeneratorProvider.DefaultPreviewSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            string text = ReadSomeText(stream, MaxCharsToRead);
            int renderSize = Math.Max(size * 4, 512);
            using var canvas = new Image<Rgba32>(renderSize, renderSize);
            float padding = renderSize * PaddingRatio;
            float wrapWidth = renderSize - (padding * 2);
            float fontSize = Math.Max(10f, renderSize * FontSizeRatio);
            var font = _fontFamily.CreateFont(fontSize, FontStyle.Regular);
            var textOptions = new RichTextOptions(font)
            {
                Origin = new PointF(padding, padding),
                WrappingLength = wrapWidth,
                LineSpacing = fontSize * LineSpacingRatio,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            var content = PrepareContent(text);
            var bodyText = BuildBodyText(content);

            var clipped = LayoutTextMonospace(bodyText, font, wrapWidth, renderSize - (padding * 2), fontSize * LineSpacingRatio);

            canvas.Mutate(ctx =>
            {
                ctx.Fill(Color.White);

                ctx.DrawText(textOptions, clipped, Color.Black);
            });

            using var output = canvas.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.TopLeft,
                Sampler = KnownResamplers.Lanczos3
            }));

            using var ms = new MemoryStream();
            await output.SaveAsWebpAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        private sealed record PreparedContent(string RawText, string NormalizedText, ContentKind Kind, int Lines);

        private enum ContentKind
        {
            Empty,
            Text,
            Binary,
        }

        private static FontFamily LoadFontFamily()
        {
            byte[] bytes = StaticFonts.GetFontBytes(StaticFontName.Consola);
            var collection = new FontCollection();
            using var fontStream = new MemoryStream(bytes, writable: false);
            return collection.Add(fontStream);
        }

        private static string ReadSomeText(Stream stream, int maxChars)
        {
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

            char[] buffer = new char[Math.Min(maxChars, 4096)];
            int total = 0;
            var sb = new System.Text.StringBuilder();

            while (total < maxChars)
            {
                int want = Math.Min(buffer.Length, maxChars - total);
                int read = reader.Read(buffer, 0, want);
                if (read <= 0)
                {
                    break;
                }

                sb.Append(buffer, 0, read);
                total += read;
            }

            return sb.Length == 0 ? string.Empty : sb.ToString();
        }

        private static PreparedContent PrepareContent(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new PreparedContent(raw, string.Empty, ContentKind.Empty, 0);
            }

            string normalized = NormalizeText(raw);
            if (LooksBinary(raw, normalized))
            {
                return new PreparedContent(raw, string.Empty, ContentKind.Binary, 0);
            }

            normalized = LimitLogicalLines(normalized, MaxLinesToRender);
            normalized = LimitLineWidth(normalized, MaxLineChars);
            int lines = CountLines(normalized);
            return new PreparedContent(raw, normalized, ContentKind.Text, lines);
        }

        private static bool LooksBinary(string raw, string normalized)
        {
            if (raw.Length == 0)
            {
                return false;
            }

            // Heuristic: if we dropped a lot of characters (NUL/control), treat as binary.
            // This avoids rendering empty previews for binary blobs mislabeled as text.
            int dropped = raw.Length - normalized.Length;
            return dropped > (raw.Length / 4);
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int lines = 1;
            foreach (var c in text)
            {
                if (c == '\n')
                {
                    lines++;
                }
            }
            return lines;
        }

        private static string BuildBodyText(PreparedContent content)
        {
            return content.Kind switch
            {
                ContentKind.Empty => string.Empty,
                ContentKind.Binary => string.Empty,
                _ => content.NormalizedText,
            };
        }

        private static string LayoutTextMonospace(
            string text,
            Font font,
            float wrapWidth,
            float maxHeight,
            float lineAdvance)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Use a single glyph measure as a character cell width.
            // For a monospaced font (Consolas) this is stable and fast.
            var m = TextMeasurer.MeasureSize("M", new RichTextOptions(font));
            float charWidth = Math.Max(1f, m.Width);

            int cols = Math.Max(1, (int)Math.Floor(wrapWidth / charWidth));
            int rows = Math.Max(1, (int)Math.Floor(maxHeight / Math.Max(1f, lineAdvance)));

            var rawLines = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            var sb = new System.Text.StringBuilder();
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

            var sb = new System.Text.StringBuilder(text.Length);
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
                    // Avoid huge single-line payloads (minified json/xml) that make measuring unreliable
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

        private static string ClipTextToFitHeight(string text, RichTextOptions options, float maxHeight)
        {
            var size = TextMeasurer.MeasureSize(text, options);
            if (size.Height <= maxHeight)
            {
                return text;
            }

            const int minKeep = 64;
            if (text.Length <= minKeep)
            {
                return text.TrimEnd() + "\n…";
            }

            int lo = 0;
            int hi = text.Length;

            while (lo + 1 < hi)
            {
                int mid = (lo + hi) / 2;
                int cut = Math.Max(minKeep, mid);
                if (cut >= hi)
                {
                    cut = hi - 1;
                }
                if (cut <= lo)
                {
                    break;
                }

                string candidate = text[..cut].TrimEnd() + "\n…";
                var s = TextMeasurer.MeasureSize(candidate, options);

                if (s.Height <= maxHeight)
                {
                    lo = cut;
                }
                else
                {
                    hi = cut;
                }
            }

            int finalLen = Math.Clamp(lo, minKeep, text.Length);
            return text[..finalLen].TrimEnd() + "\n…";
        }
    }
}
