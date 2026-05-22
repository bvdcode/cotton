// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using PhotoSauce.MagicScaler;

namespace Cotton.Previews
{
    /// <summary>
    /// Generates previews for HEIC and HEIF image formats.
    /// </summary>
    public class HeicPreviewGenerator : IPreviewGenerator
    {
        /// <inheritdoc />
        public int Version => 1;

        /// <inheritdoc />
        public IEnumerable<string> SupportedContentTypes =>
        [
            "image/heic",
            "image/heic-sequence",
            "image/heif",
            "image/heif-sequence"
        ];

        /// <inheritdoc />
        public Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
            PreviewCodecBootstrap.EnsureInitialized();

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var outputStream = new MemoryStream();
            var settings = new ProcessImageSettings
            {
                Width = size,
                Height = size,
                ResizeMode = CropScaleMode.Max
            };

            settings.TrySetEncoderFormat(ImageMimeTypes.Webp);
            MagicImageProcessor.ProcessImage(stream, outputStream, settings);

            return Task.FromResult(outputStream.ToArray());
        }
    }
}
