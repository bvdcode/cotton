using SkiaSharp;
using Svg.Skia;
using System.IO.Compression;

namespace Cotton.Previews
{
    public sealed class SvgPreviewGenerator : IPreviewGenerator
    {
        public int Version => 1;

        public IEnumerable<string> SupportedContentTypes =>
        [
            "image/svg+xml",
            "application/svg+xml"
        ];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            using MemoryStream svgContent = await ReadSvgContentAsync(stream).ConfigureAwait(false);
            using var svg = new SKSvg();
            svg.Load(svgContent);

            SKPicture? picture = svg.Picture ?? throw new InvalidOperationException("Unable to parse SVG image.");
            SKRect bounds = picture.CullRect;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                throw new InvalidOperationException("SVG image has invalid dimensions.");
            }

            float scale = Math.Min(size / bounds.Width, size / bounds.Height);
            int targetWidth = Math.Max(1, (int)Math.Round(bounds.Width * scale));
            int targetHeight = Math.Max(1, (int)Math.Round(bounds.Height * scale));

            using SKSurface surface = SKSurface.Create(new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
            canvas.Flush();

            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Webp, quality: 90);

            return data.ToArray();
        }

        private static async Task<MemoryStream> ReadSvgContentAsync(Stream stream)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var source = new MemoryStream();
            await stream.CopyToAsync(source).ConfigureAwait(false);
            source.Position = 0;

            if (!LooksLikeGZip(source))
            {
                return source;
            }

            var decompressed = new MemoryStream();
            source.Position = 0;
            using (var gzip = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true))
            {
                await gzip.CopyToAsync(decompressed).ConfigureAwait(false);
            }

            source.Dispose();
            decompressed.Position = 0;
            return decompressed;
        }

        private static bool LooksLikeGZip(MemoryStream stream)
        {
            if (stream.Length < 2)
            {
                return false;
            }

            long originalPosition = stream.Position;
            stream.Position = 0;
            int b1 = stream.ReadByte();
            int b2 = stream.ReadByte();
            stream.Position = originalPosition;
            return b1 == 0x1F && b2 == 0x8B;
        }
    }
}
