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
        private const float PaddingRatio = 0.06f;
        private const float FontSizeRatio = 0.045f;
        private const float LineSpacingRatio = 1.25f;

        private static readonly FontFamily _fontFamily = LoadFontFamily();

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = PreviewGeneratorProvider.DefaultPreviewSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

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

            text = NormalizeText(text);
            text = LimitLogicalLines(text, MaxLinesToRender);
            text = ClipTextToFitHeight(text, textOptions, maxHeight: renderSize - (padding * 2));
            canvas.Mutate(ctx =>
            {
                ctx.DrawText(textOptions, text, Color.Black);
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
