// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using Mediana.Core.Metadata;
using System.Globalization;
using System.Text;

namespace Cotton.Previews
{
    internal class MediaMetadataTagCollector
    {
        private const int MaxValueBytes = 4 * 1024;
        private const int MaxTotalBytes = 4 * 1024 * 1024;
        private int _totalBytes;

        public Dictionary<string, string> Collect(
            IReadOnlyList<MediaTag>? tags, IReadOnlyList<MediaDateTimeTag>? dates,
            IReadOnlyCollection<string> allowedNames)
        {
            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (MediaTag tag in tags ?? [])
            {
                string name = NormalizeName(tag.Name);
                if (allowedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    Add(result, name, tag.Values[0]);
                }
            }
            if (allowedNames.Contains("creation_time"))
            {
                foreach (MediaDateTimeTag date in dates ?? [])
                {
                    if (date.Name == "creation_time")
                    {
                        Add(result, date.Name, date.Value.ToString("O", CultureInfo.InvariantCulture));
                    }
                }
            }
            return result;
        }

        public void Add(Dictionary<string, string> tags, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || tags.ContainsKey(name))
            {
                return;
            }
            value = value.Trim();
            int valueBytes = Encoding.UTF8.GetByteCount(value);
            if (valueBytes > MaxValueBytes)
            {
                throw new MediaMetadataLimitExceededException("individual tag", MaxValueBytes);
            }
            _totalBytes += valueBytes + Encoding.UTF8.GetByteCount(name);
            if (_totalBytes > MaxTotalBytes)
            {
                throw new MediaMetadataLimitExceededException("aggregate tags", MaxTotalBytes);
            }
            tags.Add(name, value);
        }

        private static string NormalizeName(string name) => name.ToLowerInvariant() switch
        {
            "tit2" or "©nam" => "title",
            "tpe1" or "©art" => "artist",
            "tpe2" or "aart" or "albumartist" => "album_artist",
            "talb" or "©alb" => "album",
            "tcom" or "©wrt" => "composer",
            "trck" or "trkn" or "tracknumber" or "track_number" => "track",
            "tpos" or "disk" or "discnumber" or "disc_number" => "disc",
            "tdrc" or "©day" => "date",
            "tyer" => "year",
            "tcon" or "©gen" => "genre",
            "©xyz" => "location",
            _ => name,
        };
    }
}
