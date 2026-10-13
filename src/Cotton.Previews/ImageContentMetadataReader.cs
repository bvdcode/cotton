// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.FileType;
using Microsoft.Extensions.Logging;
using System.Globalization;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Cotton.Previews
{
    public static class ImageContentMetadataReader
    {
        public static async Task<IReadOnlyDictionary<string, string>> ReadAsync(Stream stream, ILogger logger, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(logger);
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(Path.GetTempPath(), $"cotton_metadata_{Guid.NewGuid():N}.tmp");
            await using FileStream buffer = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return Read(buffer, logger, cancellationToken);
        }

        public static IReadOnlyDictionary<string, string> Read(Stream stream, ILogger logger, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(logger);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<MetadataDirectory> directories;
            try
            {
                directories = ImageMetadataReader.ReadMetadata(stream);
            }
            catch (IOException ex) when (stream.CanSeek && stream.Position >= stream.Length)
            {
                logger.LogDebug(ex, "Image metadata ended before the reader finished parsing it.");
                throw new TruncatedImageMetadataException(ex);
            }
            return Flatten(directories, logger, cancellationToken);
        }

        internal static IReadOnlyDictionary<string, string> Flatten(
            IReadOnlyList<MetadataDirectory> directories, ILogger logger, CancellationToken cancellationToken)
        {
            Dictionary<string, string> result = new(StringComparer.Ordinal);
            HashSet<string> processedTags = new(StringComparer.Ordinal);

            foreach (MetadataDirectory directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ImageMetadataFields.AddDimensions(result, directory);
                if (directory is FileTypeDirectory fileType)
                {
                    FlatMetadataWriter.WriteText(result, "image.format", fileType.GetString(FileTypeDirectory.TagDetectedFileTypeName));
                }
                string directoryKey = $"image.{FlatMetadataWriter.EscapeKey(directory.Name)}.0";
                foreach (Tag tag in directory.Tags)
                {
                    if (!ImageMetadataFields.Includes(directory, tag.Type))
                    {
                        continue;
                    }
                    string key = $"{directoryKey}.tags.{FlatMetadataWriter.EscapeKey(tag.Name)}.{tag.Type.ToString(CultureInfo.InvariantCulture)}";
                    if (!processedTags.Add(key))
                    {
                        continue;
                    }
                    ImageMetadataValueWriter.Write(result, key, directory.GetObject(tag.Type), cancellationToken);
                    string? description = GetDescription(tag, directory, logger);
                    if (description is not null)
                    {
                        FlatMetadataWriter.WriteText(result, $"{key}.description", description);
                    }
                }

                if (directory is GpsDirectory gps && gps.GetGeoLocation() is GeoLocation location)
                {
                    result.TryAdd($"{directoryKey}.latitude", location.Latitude.ToString("R", CultureInfo.InvariantCulture));
                    result.TryAdd($"{directoryKey}.longitude", location.Longitude.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            return result;
        }

        private static string? GetDescription(Tag tag, MetadataDirectory directory, ILogger logger)
        {
            try
            {
                return tag.Description;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                logger.LogWarning(ex, "Could not describe image metadata tag {TagType} in {DirectoryName}.",
                    tag.Type, directory.Name);
                return null;
            }
        }
    }
}
