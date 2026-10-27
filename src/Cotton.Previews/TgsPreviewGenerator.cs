// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Render.NET.Svg;
using Render.NET.Tgs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using PixelBuffer = Render.NET.PixelBuffer;

namespace Cotton.Previews
{
    public class TgsPreviewGenerator : IPreviewGenerator
    {
        private const int MaxJsonBytes = 8 * 1024 * 1024;
        private const int RenderTimeoutSeconds = 20;
        private const int MaxSupersampledSize = 2048;

        public string Id => "tgs";

        public int Version => 2;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes => [AnimatedStickerContentTypes.Tgs];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(RenderTimeoutSeconds));

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using GZipStream gzip = new(stream, CompressionMode.Decompress, leaveOpen: true);
            TgsDocument document = await TgsReader.ReadAsync(gzip, new TgsReaderOptions
            {
                MaximumInputBytes = MaxJsonBytes,
                MaximumJsonBytes = MaxJsonBytes,
            }, timeout.Token).ConfigureAwait(false);
            double frame = document.InPoint + (document.OutPoint - document.InPoint) * 0.5;
            PixelBuffer pixels = TgsRenderer.RenderFrame(document, frame, new SvgRenderOptions
            {
                Width = size,
                Height = size,
                Supersampling = size <= MaxSupersampledSize ? 2 : 1,
            }, timeout.Token);
            using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixels.Pixels.Span, pixels.Width, pixels.Height);
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size).ConfigureAwait(false);
        }
    }
}
