// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.Codecs;
using Mediana.Codecs.Bmp;
using Mediana.Codecs.Gif;
using Mediana.Codecs.Jpeg;
using Mediana.Codecs.Png;
using Mediana.Codecs.Tiff;
using Mediana.Codecs.Webp;
using Mediana.Documents.Presentation;
using Mediana.Drawing;
using Mediana.Drawing.Emoji.Fluent;
using Mediana.Processing;
using SixLabors.ImageSharp;
using Rgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Cotton.Previews
{
    public class PresentationPreviewGenerator : IPreviewGenerator
    {
        private const int MaximumSourceBytes = int.MaxValue;
        private const int MaximumPartBytes = 1024 * 1024 * 1024;
        private const long MaximumUncompressedBytes = 4L * 1024 * 1024 * 1024;
        private const int MaxSupersampledSize = 2048;
        private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(60);
        private static readonly SemaphoreSlim RenderLock = new(1, 1);
        private static readonly ImageDecodeContext Images = new([
            new BmpImageDecoder(),
            new IcoImageDecoder(),
            new GifImageDecoder(),
            new TiffImageDecoder(),
            new WebpImageDecoder(),
            new PngImageDecoder(new()
            {
                MaximumPixels = Array.MaxLength / 4,
                MaximumEncodedBytes = Array.MaxLength,
            }),
            new JpegImageDecoder(new()
            {
                MaximumPixels = Array.MaxLength / 4,
                MaximumEncodedBytes = Array.MaxLength,
                ChromaConflictPolicy = JpegChromaConflictPolicy.UseJfif,
            }),
        ]);

        public string Id => "office-pptx";
        public int Version => 5;
        public int Priority => 0;
        public IEnumerable<string> SupportedContentTypes => ["application/vnd.openxmlformats-officedocument.presentationml.presentation"];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            await RenderLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using CancellationTokenSource cancellation = new(RenderTimeout);
                using PresentationDocument document = PresentationReader.Open(stream, new()
                {
                    Package = new()
                    {
                        MaximumInputBytes = MaximumSourceBytes,
                        MaximumPartBytes = MaximumPartBytes,
                        MaximumUncompressedBytes = MaximumUncompressedBytes,
                    },
                }, leaveOpen: true, cancellationToken: cancellation.Token);
                using FluentEmojiProvider emoji = new();
                using PixelBuffer pixels = await PresentationRenderer.RenderAsync(document, 0, new()
                {
                    Dpi = size * 72d / Math.Max(document.Width, document.Height),
                    MaximumWidth = size,
                    MaximumHeight = size,
                    Supersampling = size <= MaxSupersampledSize ? 2 : 1,
                    Text = new()
                    {
                        SubstituteMissingFonts = true,
                        InlineGraphicProvider = emoji,
                    },
                    ImageColorResolver = SdrPreviewColorPolicy.Resolve,
                    Images = Images,
                }, cancellation.Token).ConfigureAwait(false);
                using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixels.Pixels.Span, pixels.Width, pixels.Height);
                return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size).ConfigureAwait(false);
            }
            finally
            {
                RenderLock.Release();
            }
        }
    }
}
