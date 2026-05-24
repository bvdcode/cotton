// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cotton.Previews
{
    /// <summary>
    /// Generates previews for standard raster image formats.
    /// </summary>
    public class ImagePreviewGenerator : IPreviewGenerator
    {
        /// <inheritdoc />
        public int Version => 2;
        /// <summary>WebP quality used for small image previews.</summary>
        public const int SmallPreviewQuality = 75;
        /// <summary>WebP quality used for large image previews.</summary>
        public const int LargePreviewQuality = 82;

        /// <inheritdoc />
        public IEnumerable<string> SupportedContentTypes =>
            Configuration.Default.ImageFormats.SelectMany(x => x.MimeTypes);

        /// <inheritdoc />
        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            int mid = (PreviewGeneratorProvider.DefaultSmallPreviewSize + PreviewGeneratorProvider.DefaultLargePreviewSize) / 2;
            int quality = size > mid ? LargePreviewQuality : SmallPreviewQuality;

            using Image<Rgba32> image = Image.Load<Rgba32>(stream);
            image.Mutate(x => x.AutoOrient());
            if (image.Width > size || image.Height > size)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Max
                }));
            }
            using var outputStream = new MemoryStream();

            await image.SaveAsWebpAsync(outputStream, new WebpEncoder { Quality = quality });
            return outputStream.ToArray();
        }
    }
}
