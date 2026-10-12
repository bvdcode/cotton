// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    internal static class MediaMetadataFields
    {
        public static readonly string[] StreamProperties =
        [
            "index", "codec_type", "codec_name", "profile", "width", "height", "avg_frame_rate",
            "sample_aspect_ratio", "pix_fmt", "color_space", "color_transfer", "color_primaries",
            "sample_rate", "channels", "channel_layout", "bit_rate",
        ];

        public static readonly string[] StreamTags = ["language", "title", "rotate", "creation_time"];
        public static readonly string[] FormatProperties = ["format_name", "bit_rate"];
        public static readonly string[] FormatTags =
        [
            "title", "artist", "album", "album_artist", "album artist", "albumartist", "composer", "performer",
            "track", "tracknumber", "track_number", "disc", "discnumber", "disc_number", "date", "creation_time",
            "year", "genre", "location", "location-eng", "com.apple.quicktime.location.ISO6709",
        ];

        public static string ShowEntries { get; } =
            $"format=duration,{string.Join(',', FormatProperties)}:format_tags={TagEntries(FormatTags)}"
            + $":stream={string.Join(',', StreamProperties)}:stream_tags={TagEntries(StreamTags)}:stream_side_data=rotation";

        private static string TagEntries(IEnumerable<string> tags) =>
            string.Join(',', tags.SelectMany(tag => new[] { tag, tag.ToUpperInvariant() }).Distinct(StringComparer.Ordinal));
    }
}
