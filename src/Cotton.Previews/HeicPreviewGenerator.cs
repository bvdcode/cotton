// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using LibHeifSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Cotton.Previews
{
    public class HeicPreviewGenerator : IPreviewGenerator
    {
        public string Id => "heic";

        private const int Rgba32BytesPerPixel = 4;

        private static readonly IImageFormatDetector[] ImageFormatDetectors =
        [
            new JpegImageFormatDetector(),
            new PngImageFormatDetector(),
            new GifImageFormatDetector(),
            new BmpImageFormatDetector(),
            new WebpImageFormatDetector(),
            new TiffImageFormatDetector(),
        ];

        public int Version => 3;

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

            using Image<Rgba32> image = DecodeToImage(buffer.ToArray());
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size);
        }

        private static Image<Rgba32> DecodeToImage(byte[] bytes)
        {
            using HeifContext context = new HeifContext(bytes);
            using HeifImageHandle handle = context.GetPrimaryImageHandle();
            using HeifImage decoded = handle.Decode(HeifColorspace.Rgb, HeifChroma.InterleavedRgba32);

            int width = decoded.Width;
            int height = decoded.Height;
            HeifPlaneData plane = decoded.GetPlane(HeifChannel.Interleaved);

            int rowBytes = checked(width * Rgba32BytesPerPixel);
            byte[] pixels = new byte[checked(rowBytes * height)];
            for (int y = 0; y < height; y++)
            {
                int sourceOffset = checked(y * plane.Stride);
                int targetOffset = checked(y * rowBytes);
                Marshal.Copy(IntPtr.Add(plane.Scan0, sourceOffset), pixels, targetOffset, rowBytes);
            }

            return Image.LoadPixelData<Rgba32>(pixels, width, height);
        }
    }
}
