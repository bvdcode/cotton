// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Xmp;
using System.Globalization;
using XmpCore;
using XmpCore.Options;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Cotton.Previews
{
    public static class ImageContentMetadataReader
    {
        public static async Task<IReadOnlyDictionary<string, string>> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(Path.GetTempPath(), $"cotton_metadata_{Guid.NewGuid():N}.tmp");
            await using FileStream buffer = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return Read(buffer, cancellationToken);
        }

        public static IReadOnlyDictionary<string, string> Read(Stream stream, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<MetadataDirectory> directories = ImageMetadataReader.ReadMetadata(stream);
            return Flatten(directories, cancellationToken);
        }

        internal static IReadOnlyDictionary<string, string> Flatten(
            IReadOnlyList<MetadataDirectory> directories, CancellationToken cancellationToken)
        {
            Dictionary<string, string> result = new(StringComparer.Ordinal);
            Dictionary<string, int> occurrences = new(StringComparer.Ordinal);
            Dictionary<MetadataDirectory, string> directoryKeys = new();

            foreach (MetadataDirectory directory in directories)
            {
                occurrences.TryGetValue(directory.Name, out int occurrence);
                directoryKeys.Add(directory,
                    $"image.{FlatMetadataWriter.EscapeKey(directory.Name)}.{occurrence.ToString(CultureInfo.InvariantCulture)}");
                occurrences[directory.Name] = occurrence + 1;
            }

            foreach (MetadataDirectory directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directoryKey = directoryKeys[directory];
                foreach (Tag tag in directory.Tags)
                {
                    string key = $"{directoryKey}.tags.{FlatMetadataWriter.EscapeKey(tag.Name)}.{tag.Type.ToString(CultureInfo.InvariantCulture)}";
                    ImageMetadataValueWriter.Write(result, key, directory.GetObject(tag.Type), cancellationToken);
                    string? description = tag.Description;
                    if (description is not null)
                    {
                        FlatMetadataWriter.WriteText(result, $"{key}.description", description);
                    }
                }

                int errorIndex = 0;
                foreach (string error in directory.Errors)
                {
                    FlatMetadataWriter.WriteText(result,
                        $"{directoryKey}.errors.{errorIndex.ToString(CultureInfo.InvariantCulture)}", error);
                    errorIndex++;
                }

                if (directory.Parent is not null && directoryKeys.TryGetValue(directory.Parent, out string? parentKey))
                {
                    result[$"{directoryKey}.parent"] = parentKey;
                }

                if (directory is XmpDirectory xmp)
                {
                    foreach ((string path, string value) in xmp.GetXmpProperties())
                    {
                        FlatMetadataWriter.WriteText(result, $"{directoryKey}.properties.{FlatMetadataWriter.EscapeKey(path)}", value);
                    }
                    if (xmp.XmpMeta is not null)
                    {
                        FlatMetadataWriter.WriteText(result, $"{directoryKey}.packet",
                            XmpMetaFactory.SerializeToString(xmp.XmpMeta, new SerializeOptions()));
                    }
                }

                if (directory is GpsDirectory gps && gps.GetGeoLocation() is GeoLocation location)
                {
                    result[$"{directoryKey}.latitude"] = location.Latitude.ToString("R", CultureInfo.InvariantCulture);
                    result[$"{directoryKey}.longitude"] = location.Longitude.ToString("R", CultureInfo.InvariantCulture);
                }
            }

            return result;
        }
    }
}
