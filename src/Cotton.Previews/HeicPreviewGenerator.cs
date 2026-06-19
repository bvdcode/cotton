// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using LibHeifSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Cotton.Previews
{
    /// <summary>
    /// Generates previews for HEIC and HEIF images by decoding with libheif into an
    /// ImageSharp image, then encoding through the shared <see cref="PreviewImageEncoder"/>.
    /// </summary>
    public class HeicPreviewGenerator : IPreviewGenerator
    {
        /// <inheritdoc />
        public int Version => 2;

        /// <inheritdoc />
        public IEnumerable<string> SupportedContentTypes =>
        [
            "image/heic",
            "image/heic-sequence",
            "image/heif",
            "image/heif-sequence"
        ];

        /// <inheritdoc />
        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            using Image<Rgba32> image = DecodeToImage(stream);
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size);
        }

        private static Image<Rgba32> DecodeToImage(Stream stream)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            using var context = new HeifContext(buffer.ToArray());
            using HeifImageHandle handle = context.GetPrimaryImageHandle();
            using HeifImage decoded = handle.Decode(HeifColorspace.Rgb, HeifChroma.InterleavedRgba32);

            int width = decoded.Width;
            int height = decoded.Height;
            HeifPlaneData plane = decoded.GetPlane(HeifChannel.Interleaved);

            int rowBytes = width * 4;
            byte[] pixels = new byte[rowBytes * height];
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(IntPtr.Add(plane.Scan0, y * plane.Stride), pixels, y * rowBytes, rowBytes);
            }

            return Image.LoadPixelData<Rgba32>(pixels, width, height);
        }
    }
}
