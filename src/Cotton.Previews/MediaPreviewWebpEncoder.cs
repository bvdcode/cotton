// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using Mediana.Codecs;
using Mediana.Core.Memory;
using Mediana.Core.Video;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Cotton.Previews
{
    internal class MediaPreviewWebpEncoder(int size) : IImageEncoder
    {
        public string ImplementationId => "cotton.preview.webp";
        public string FormatId => "webp";
        public ImageEncoderFeatures Features => ImageEncoderFeatures.None;

        public async Task<long> EncodeAsync(VideoFrame input, Stream destination, MediaBufferAllocator allocator,
            CancellationToken cancellationToken = default)
        {
            using Image<Rgba32> image = new(input.Format.Width, input.Format.Height);
            image.ProcessPixelRows(pixels =>
            {
                for (int row = 0; row < pixels.Height; row++)
                {
                    MemoryMarshal.Cast<byte, Rgba32>(input.GetRow(0, row)).CopyTo(pixels.GetRowSpan(row));
                }
            });

            long start = destination.Position;
            await PreviewImageEncoder.SaveAsync(image, destination, size, cancellationToken).ConfigureAwait(false);
            return destination.Position - start;
        }
    }
}
