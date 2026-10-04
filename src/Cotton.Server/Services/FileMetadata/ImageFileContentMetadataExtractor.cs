// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Cotton.Previews;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using System.Globalization;

namespace Cotton.Server.Services.FileMetadata
{
    internal class ImageFileContentMetadataExtractor(ILogger<ImageFileContentMetadataExtractor> logger) : IFileContentMetadataExtractor
    {
        public static readonly IReadOnlyCollection<string> SupportedContentTypes =
        [
            .. Configuration.Default.ImageFormats.SelectMany(x => x.MimeTypes)
                .Concat(RawImageContentTypes.All)
                .Concat(["image/heic", "image/heif", "image/heic-sequence", "image/heif-sequence", "image/avif",
                    "image/vnd.adobe.photoshop", "image/x-photoshop", "image/x-pcx", "image/x-icon"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        ];

        public bool Supports(string contentType) =>
            SupportedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

        public async Task<IReadOnlyDictionary<string, string>> ExtractAsync(
            Stream stream,
            string contentType,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            Dictionary<string, string> result = new(StringComparer.Ordinal);
            try
            {
                IReadOnlyDictionary<string, string> metadata = await ImageContentMetadataReader.ReadAsync(stream, cancellationToken);
                foreach ((string key, string value) in metadata)
                {
                    result.Add(key, value);
                }
            }
            catch (MetadataExtractor.ImageProcessingException ex)
            {
                logger.LogDebug(ex, "Image metadata reader could not identify content type {ContentType}.", contentType);
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            if (!Configuration.Default.ImageFormats.SelectMany(format => format.MimeTypes)
                .Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return result;
            }

            ImageInfo? info;
            try
            {
                info = await Image.IdentifyAsync(stream, cancellationToken);
            }
            catch (UnknownImageFormatException)
            {
                return result;
            }
            catch (InvalidImageContentException)
            {
                return result;
            }

            if (info is null)
            {
                return result;
            }

            result[FileContentMetadataKeys.ImageWidth] = info.Width.ToString(CultureInfo.InvariantCulture);
            result[FileContentMetadataKeys.ImageHeight] = info.Height.ToString(CultureInfo.InvariantCulture);

            IImageFormat? format = info.Metadata.DecodedImageFormat;
            if (format is not null)
            {
                result[FileContentMetadataKeys.ImageFormat] = format.Name;
            }

            return result;
        }
    }
}
