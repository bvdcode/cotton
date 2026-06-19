// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp.Formats.Webp;

namespace Cotton.Previews
{
    /// <summary>
    /// Single source of truth for how preview images are encoded to WebP.
    /// All ImageSharp-based preview generators must route through here so the
    /// lossy/lossless policy and quality settings live in one place.
    /// </summary>
    /// <remarks>
    /// Generators backed by other codecs (HEIC via MagicScaler, SVG via SkiaSharp)
    /// encode through their own libraries and also emit lossy WebP.
    /// </remarks>
    public static class PreviewImageEncoder
    {
        /// <summary>WebP quality used for small previews (thumbnails and the social/OG image).</summary>
        public const int SmallPreviewQuality = 75;
        /// <summary>WebP quality used for large previews (in-app full-size viewer).</summary>
        public const int LargePreviewQuality = 85;

        /// <summary>
        /// Builds the WebP encoder for a preview of the given target size.
        /// </summary>
        /// <remarks>
        /// Always lossy (VP8). Without an explicit <see cref="WebpEncoder.FileFormat"/>,
        /// ImageSharp mirrors the decoded source format, so lossless sources (PNG video
        /// frames, rendered model/PDF pages, PNG uploads) produce lossless VP8L previews.
        /// Social crawlers such as Telegram's link-preview bot cannot decode VP8L, leaving
        /// shared links with text but no image; lossy is also far smaller for photographic
        /// and thumbnail content.
        /// </remarks>
        public static WebpEncoder Create(int size)
        {
            int mid = (PreviewGeneratorProvider.DefaultSmallPreviewSize
                + PreviewGeneratorProvider.DefaultLargePreviewSize) / 2;
            int quality = size > mid ? LargePreviewQuality : SmallPreviewQuality;
            return new WebpEncoder
            {
                Quality = quality,
                FileFormat = WebpFileFormatType.Lossy,
            };
        }
    }
}
