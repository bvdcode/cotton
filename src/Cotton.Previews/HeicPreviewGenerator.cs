// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Heic.Net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cotton.Previews
{
    public class HeicPreviewGenerator : IPreviewGenerator
    {
        public string Id => "heic";

        private static readonly IImageFormatDetector[] ImageFormatDetectors =
        [
            new JpegImageFormatDetector(),
            new PngImageFormatDetector(),
            new GifImageFormatDetector(),
            new BmpImageFormatDetector(),
            new WebpImageFormatDetector(),
            new TiffImageFormatDetector(),
        ];

        public int Version => 4;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes =>
        [
            "image/heic",
            "image/heic-sequence",
            "image/heif",
            "image/heif-sequence"
        ];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer).ConfigureAwait(false);

            foreach (IImageFormatDetector detector in ImageFormatDetectors)
            {
                if (detector.TryDetectFormat(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), out _))
                {
                    return await new ImagePreviewGenerator().GeneratePreviewWebPAsync(buffer, size);
                }
            }

            using Image<Rgba32> image = DecodeToImage(buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length)));
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size);
        }

        private static Image<Rgba32> DecodeToImage(ReadOnlyMemory<byte> bytes)
        {
            HeicDocument document = HeicDocument.Load(bytes);
            RgbaImage decoded = document.DecodePrimary();
            return Image.LoadPixelData<Rgba32>(decoded.Pixels.Span, decoded.Width, decoded.Height);
        }
    }
}
