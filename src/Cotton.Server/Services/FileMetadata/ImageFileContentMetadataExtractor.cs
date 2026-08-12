// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using System.Globalization;

namespace Cotton.Server.Services.FileMetadata
{
    internal class ImageFileContentMetadataExtractor : IFileContentMetadataExtractor
    {
        public static readonly IReadOnlyCollection<string> SupportedContentTypes =
        [
            .. Configuration.Default.ImageFormats
                .SelectMany(x => x.MimeTypes)
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

            ImageInfo? info;
            try
            {
                info = await Image.IdentifyAsync(stream, cancellationToken);
            }
            catch (UnknownImageFormatException)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
            catch (InvalidImageContentException)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            if (info is null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            Dictionary<string, string> result = new(StringComparer.Ordinal)
            {
                [FileContentMetadataKeys.ImageWidth] = info.Width.ToString(CultureInfo.InvariantCulture),
                [FileContentMetadataKeys.ImageHeight] = info.Height.ToString(CultureInfo.InvariantCulture),
            };

            IImageFormat? format = info.Metadata.DecodedImageFormat;
            if (format is not null)
            {
                result[FileContentMetadataKeys.ImageFormat] = format.Name;
            }

            return result;
        }
    }
}
