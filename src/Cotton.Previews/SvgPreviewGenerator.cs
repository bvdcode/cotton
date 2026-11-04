// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Render.NET.Svg;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using PixelBuffer = Render.NET.PixelBuffer;

namespace Cotton.Previews
{
    public class SvgPreviewGenerator : IPreviewGenerator
    {
        private const int RenderTimeoutSeconds = 20;
        private const int MaxSupersampledSize = 2048;

        public string Id => "svg";

        public int Version => 3;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes =>
        [
            "image/svg+xml",
            "application/svg+xml"
        ];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(RenderTimeoutSeconds));
            SvgDocument document = await ReadDocumentAsync(stream, timeout.Token).ConfigureAwait(false);
            double scale = size / Math.Max(document.Width, document.Height);
            PixelBuffer pixels = SvgRenderer.Render(document, new SvgRenderOptions
            {
                Width = Math.Max(1, (int)Math.Round(document.Width * scale)),
                Height = Math.Max(1, (int)Math.Round(document.Height * scale)),
                Supersampling = size <= MaxSupersampledSize ? 2 : 1,
            }, timeout.Token);
            using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixels.Pixels.Span, pixels.Width, pixels.Height);
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size).ConfigureAwait(false);
        }

        private static async Task<SvgDocument> ReadDocumentAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            SvgReaderOptions options = new();
            using MemoryStream source = new();
            await PreviewStream.CopyBoundedAsync(stream, source, options.MaximumInputBytes, cancellationToken).ConfigureAwait(false);
            if (source.Length >= 2 && source.GetBuffer()[0] == 0x1F && source.GetBuffer()[1] == 0x8B)
            {
                using GZipStream gzip = new(source, CompressionMode.Decompress, leaveOpen: true);
                return await SvgReader.ReadAsync(gzip, options, cancellationToken).ConfigureAwait(false);
            }

            return await SvgReader.ReadAsync(source, options, cancellationToken).ConfigureAwait(false);
        }
    }
}
