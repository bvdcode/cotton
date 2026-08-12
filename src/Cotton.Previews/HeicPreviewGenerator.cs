// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using LibHeifSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Cotton.Previews
{
    public class HeicPreviewGenerator : IPreviewGenerator
    {
        private const int Rgba32BytesPerPixel = 4;

        public int Version => 2;

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

            using Image<Rgba32> image = await DecodeToImageAsync(stream).ConfigureAwait(false);
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size);
        }

        private static async Task<Image<Rgba32>> DecodeToImageAsync(Stream stream)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer).ConfigureAwait(false);

            using var context = new HeifContext(buffer.ToArray());
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
