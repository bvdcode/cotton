// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.Documents.Word;
using Mediana.Drawing;
using Mediana.Drawing.Emoji.Fluent;
using Mediana.Processing;
using SixLabors.ImageSharp;
using Rgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Cotton.Previews
{
    public class WordPreviewGenerator : IPreviewGenerator
    {
        private const long MaximumSourceBytes = 64 * 1024 * 1024;
        private const int RenderTimeoutSeconds = 60;
        private const int MaxSupersampledSize = 2048;
        private static readonly WordReaderOptions ReaderOptions = new()
        {
            AllowFractionalMeasurements = true,
            DefaultPageSize = WordPageSize.A4,
        };

        public string Id => "office-docx";
        public int Version => 3;
        public int Priority => 0;
        public IEnumerable<string> SupportedContentTypes => ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(RenderTimeoutSeconds));
            using MemoryStream input = new();
            await PreviewStream.CopyBoundedAsync(stream, input, MaximumSourceBytes, timeout.Token).ConfigureAwait(false);
            WordDocument document = WordReader.Read(input.GetBuffer().AsMemory(0, (int)input.Length), ReaderOptions, timeout.Token);
            using FluentEmojiProvider emoji = new();
            WordLayout layout = WordRenderer.Layout(document, new WordLayoutOptions
            {
                Text = new()
                {
                    SubstituteMissingFonts = true,
                    InlineGraphicProvider = emoji,
                },
            }, timeout.Token);
            WordPageSize page = layout.GetPageSize(1);
            using PixelBuffer pixels = await WordRenderer.RenderAsync(layout, 1, new WordRenderOptions
            {
                Dpi = size * 72d / Math.Max(page.Width, page.Height),
                MaximumWidth = size,
                MaximumHeight = size,
                Supersampling = size <= MaxSupersampledSize ? 2 : 1,
                ImageColorResolver = SdrPreviewColorPolicy.Resolve,
            }, timeout.Token).ConfigureAwait(false);
            using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixels.Pixels.Span, pixels.Width, pixels.Height);
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size).ConfigureAwait(false);
        }
    }
}
