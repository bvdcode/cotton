// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Heif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Photoshop;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.WebP;
using System.Globalization;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Cotton.Previews
{
    internal static class ImageMetadataFields
    {
        private static readonly HashSet<int> ExifTags =
        [
            ExifDirectoryBase.TagMake, ExifDirectoryBase.TagModel, ExifDirectoryBase.TagOrientation,
            ExifDirectoryBase.TagDateTime, ExifDirectoryBase.TagDateTimeOriginal,
            ExifDirectoryBase.TagExposureTime, ExifDirectoryBase.TagFNumber,
            ExifDirectoryBase.TagIsoEquivalent, ExifDirectoryBase.TagExposureBias,
            ExifDirectoryBase.TagFlash, ExifDirectoryBase.TagFocalLength,
            ExifDirectoryBase.Tag35MMFilmEquivFocalLength, ExifDirectoryBase.TagWhiteBalanceMode,
            ExifDirectoryBase.TagLensMake, ExifDirectoryBase.TagLensModel,
        ];

        private static readonly HashSet<int> GpsTags =
        [
            GpsDirectory.TagLatitudeRef, GpsDirectory.TagLatitude,
            GpsDirectory.TagLongitudeRef, GpsDirectory.TagLongitude,
            GpsDirectory.TagAltitudeRef, GpsDirectory.TagAltitude,
        ];

        public static bool Includes(MetadataDirectory directory, int tag) => directory switch
        {
            ExifIfd0Directory or ExifSubIfdDirectory => ExifTags.Contains(tag),
            GpsDirectory => GpsTags.Contains(tag),
            _ => false,
        };

        public static void AddDimensions(Dictionary<string, string> target, MetadataDirectory directory)
        {
            (int Width, int Height)[] candidates = directory switch
            {
                HeicImagePropertiesDirectory => [(HeicImagePropertiesDirectory.TagImageWidth, HeicImagePropertiesDirectory.TagImageHeight)],
                JpegDirectory => [(JpegDirectory.TagImageWidth, JpegDirectory.TagImageHeight)],
                PngDirectory => [(PngDirectory.TagImageWidth, PngDirectory.TagImageHeight)],
                PsdHeaderDirectory => [(PsdHeaderDirectory.TagImageWidth, PsdHeaderDirectory.TagImageHeight)],
                WebPDirectory => [(WebPDirectory.TagImageWidth, WebPDirectory.TagImageHeight)],
                ExifIfd0Directory or ExifImageDirectory => [(ExifDirectoryBase.TagImageWidth, ExifDirectoryBase.TagImageHeight)],
                ExifSubIfdDirectory =>
                [
                    (ExifDirectoryBase.TagExifImageWidth, ExifDirectoryBase.TagExifImageHeight),
                    (ExifDirectoryBase.TagImageWidth, ExifDirectoryBase.TagImageHeight),
                ],
                _ => [],
            };
            foreach ((int widthTag, int heightTag) in candidates)
            {
                if (!directory.TryGetInt32(widthTag, out int width) || width <= 0
                    || !directory.TryGetInt32(heightTag, out int height) || height <= 0)
                {
                    continue;
                }
                int.TryParse(target.GetValueOrDefault("image.width"), CultureInfo.InvariantCulture, out int previousWidth);
                int.TryParse(target.GetValueOrDefault("image.height"), CultureInfo.InvariantCulture, out int previousHeight);
                if ((long)width * height > (long)previousWidth * previousHeight)
                {
                    ImageMetadataValueWriter.Write(target, "image.width", width, CancellationToken.None);
                    ImageMetadataValueWriter.Write(target, "image.height", height, CancellationToken.None);
                }
            }
        }
    }
}
