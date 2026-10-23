// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Lokad.OoxPdf;
using SixLabors.ImageSharp;

namespace Cotton.Previews
{
    public class OfficePreviewGenerator(OoxPdfInputKind inputKind, string contentType) : IPreviewGenerator
    {
        private const long MaximumSourceBytes = 64 * 1024 * 1024;
        private const long MaximumPdfBytes = 32 * 1024 * 1024;
        private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(60);
        private static readonly SemaphoreSlim RenderLock = new(1, 1);
        private static readonly PdfPreviewGenerator PdfGenerator = new(Color.White);

        public string Id => "office-" + inputKind.ToString().ToLowerInvariant();
        public int Version => 0;
        public int Priority => 0;
        public IEnumerable<string> SupportedContentTypes => [contentType];

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            await RenderLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using CancellationTokenSource cancellation = new(RenderTimeout);
                using MemoryStream input = new();
                await PreviewStream.CopyBoundedAsync(stream, input, MaximumSourceBytes, cancellation.Token).ConfigureAwait(false);
                using MemoryStream pdf = new();
                await OoxPdfConverter.ConvertAsync(input, pdf, new OoxPdfOptions
                {
                    InputKind = inputKind,
                    FontResolver = new OfficePreviewFontResolver(),
                }, cancellation.Token).ConfigureAwait(false);
                if (pdf.Length > MaximumPdfBytes)
                {
                    throw new InvalidDataException("Converted document exceeds the supported preview size limit.");
                }
                pdf.Position = 0;
                return await PdfGenerator.GeneratePreviewWebPAsync(pdf, size).ConfigureAwait(false);
            }
            finally
            {
                RenderLock.Release();
            }
        }
    }
}
