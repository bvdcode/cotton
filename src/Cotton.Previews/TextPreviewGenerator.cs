// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Fonts.Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    /// <summary>
    /// Generates previews for text-like documents.
    /// </summary>
    public class TextPreviewGenerator : IPreviewGenerator
    {
        /// <inheritdoc />
        public int Version => 0;
        /// <inheritdoc />
        public IEnumerable<string> SupportedContentTypes =>
        [
            "text/plain",
            "text/markdown",
            "text/x-csharp",
            "application/xml",
            "application/json",
            "application/javascript",
        ];

        private const int MaxCharsToRead = 24_000;
        private const float PaddingRatio = 0.06f;
        private const float FontSizeRatio = 0.045f;

        private static readonly FontFamily _fontFamily = LoadFontFamily();

        /// <inheritdoc />
        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            string text;
            using (var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                char[] buffer = new char[Math.Min(MaxCharsToRead, 8192)];
                int total = 0;
                var sb = new System.Text.StringBuilder();

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

            if (string.IsNullOrWhiteSpace(text))
            {
                text = "(empty file)";
            }

            text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

            const int maxChars = 4000;
            if (text.Length > maxChars)
            {
                text = text[..maxChars] + "\n…";
            }

            int renderSize = Math.Max(size * 4, 512);
            using var canvas = new Image<Rgba32>(renderSize, renderSize);
            float padding = renderSize * PaddingRatio;
            float paddingTop = padding * 1.3f;
            float fontSize = Math.Max(10f, renderSize * FontSizeRatio);
            var font = _fontFamily.CreateFont(fontSize, FontStyle.Regular);

            canvas.Mutate(ctx =>
            {
                ctx.BackgroundColor(Color.White);
                ctx.Paint(canvas => canvas.DrawText(
                    new RichTextOptions(font) { Origin = new PointF(padding, paddingTop) },
                    text,
                    Brushes.Solid(Color.Black),
                    null));
            });

            using var output = canvas.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.TopLeft,
                Sampler = KnownResamplers.Lanczos3
            }));

            using var ms = new MemoryStream();
            await output.SaveAsWebpAsync(ms, PreviewImageEncoder.Create(size)).ConfigureAwait(false);
            return ms.ToArray();
        }

        private static FontFamily LoadFontFamily()
        {
            byte[] bytes = StaticFonts.GetFontBytes(StaticFontName.Consola);
            var collection = new FontCollection();
            using var fontStream = new MemoryStream(bytes, writable: false);
            return collection.Add(fontStream);
        }
    }
}
