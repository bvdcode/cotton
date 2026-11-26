// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    internal static class MediaMetadataFields
    {
        public static readonly string[] StreamTags = ["language", "title", "rotate", "creation_time"];
        public static readonly string[] LocationTags = ["location", "location-eng", "com.apple.quicktime.location.ISO6709"];
        public static readonly string[] FormatTags =
        [
            "title", "artist", "album", "album_artist", "album artist", "albumartist", "composer", "performer",
            "track", "tracknumber", "track_number", "disc", "discnumber", "disc_number", "date", "creation_time",
            "year", "genre", .. LocationTags,
        ];
    }
}
