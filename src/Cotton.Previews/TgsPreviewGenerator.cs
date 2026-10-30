// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using SkiaSharp;
using SkiaSharp.Skottie;
using System.IO.Compression;
using System.Text.Json;

namespace Cotton.Previews
{
    public class TgsPreviewGenerator : IPreviewGenerator
    {
        private const int MaxJsonBytes = 8 * 1024 * 1024;

        public string Id => "tgs";

        public int Version => 1;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes => [AnimatedStickerContentTypes.Tgs];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using GZipStream gzip = new(stream, CompressionMode.Decompress, leaveOpen: true);
            using MemoryStream json = new();
            byte[] buffer = new byte[8192];
            while (true)
            {
                int count = await gzip.ReadAsync(buffer).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }
                if (json.Length + count > MaxJsonBytes)
                {
                    throw new InvalidDataException("The TGS animation exceeds the supported size.");
                }
                await json.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
            }

            using JsonDocument document = JsonDocument.Parse(json.GetBuffer().AsMemory(0, (int)json.Length));
            using Animation animation = Animation.Parse(JsonSerializer.Serialize(document.RootElement))
                ?? throw new InvalidDataException("The TGS animation is invalid.");
            if (animation.Size.Width <= 0 || animation.Size.Height <= 0)
            {
                throw new InvalidDataException("The TGS animation has invalid dimensions.");
            }

            using SKSurface surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul))
                ?? throw new InvalidOperationException("Unable to create a TGS preview surface.");
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            float scale = Math.Min(size / animation.Size.Width, size / animation.Size.Height);
            float width = animation.Size.Width * scale;
            float height = animation.Size.Height * scale;
            SKRect bounds = new((size - width) / 2, (size - height) / 2,
                (size + width) / 2, (size + height) / 2);
            animation.Seek(0.5);
            animation.Render(canvas, bounds);
            canvas.Flush();

            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Webp, quality: 90);
            return data.ToArray();
        }
    }
}
