// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Mediana.Core.Memory;
using Mediana.Core.Video;
using Mediana.Images.Metadata;
using Mediana.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cotton.Previews
{
    public class RawPreviewGenerator : IPreviewGenerator
    {
        private const int MaxSourceBytes = 512 * 1024 * 1024;
        private const int DecodeBufferBudgetBytes = 256 * 1024 * 1024;

        public string Id => "raw";

        public int Version => 3;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes => RawImageContentTypes.All;

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            RawImagePreview preview = await RawPreviewReader.ExtractAsync(stream, size, MaxSourceBytes)
                ?? throw new InvalidDataException("RAW file has no supported embedded JPEG image.");

            MediaBufferAllocator allocator = new(DecodeBufferBudgetBytes);
            using VideoFrame decoded = preview.DecodeForPreview(allocator);
            using VideoFrame frame = SrgbConverter.ApplyWithColorPolicy(decoded, SdrPreviewColorPolicy.Resolve, allocator);
            using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(frame.Data.Span, frame.Format.Width, frame.Format.Height);
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size);
        }
    }
}
