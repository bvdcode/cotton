// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.Documents.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using PixelBuffer = Mediana.Drawing.PixelBuffer;

namespace Cotton.Previews
{
    public class PdfPreviewGenerator(Color? backgroundColor = null) : IPreviewGenerator
    {
        private const int RenderTimeoutSeconds = 20;
        private const int MaximumDpi = 2400;
        private const int MaxSupersampledSize = 2048;
        private static readonly PdfReaderOptions ReaderOptions = new()
        {
            Text = new()
            {
                SubstituteMissingFonts = true,
            },
        };

        public string Id => "pdf";

        public int Version => 3;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes => ["application/pdf"];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(RenderTimeoutSeconds));
            using PdfDocument document = await PdfReader.ReadAsync(stream, ReaderOptions, timeout.Token).ConfigureAwait(false);
            if (document.PageCount == 0)
            {
                throw new InvalidOperationException("PDF has no pages.");
            }

            PdfPageInfo page = document.GetPageInfo(1);
            Rgba32 background = backgroundColor.GetValueOrDefault(Color.Transparent).ToPixel<Rgba32>();
            using PixelBuffer pixels = PdfRenderer.Render(document, 1, new PdfRenderOptions
            {
                Dpi = Math.Min(MaximumDpi, size * 72d / Math.Max(page.Width, page.Height)),
                MaximumWidth = size,
                MaximumHeight = size,
                Supersampling = size <= MaxSupersampledSize ? 2 : 1,
                Background = new Mediana.Drawing.Rgba32(background.R, background.G, background.B, background.A),
            }, timeout.Token);
            using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixels.Pixels.Span, pixels.Width, pixels.Height);
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size).ConfigureAwait(false);
        }
    }
}
