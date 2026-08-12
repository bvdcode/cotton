// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    internal static class FileContentMetadataKeys
    {
        public const string ExtractionProcessed = "contentMetadata.extractionProcessed";

        public const string ImageWidth = "image.width";
        public const string ImageHeight = "image.height";
        public const string ImageFormat = "image.format";

        public const string MediaTitle = "media.title";
        public const string MediaArtist = "media.artist";
        public const string MediaAlbum = "media.album";
        public const string MediaAlbumArtist = "media.albumArtist";
        public const string MediaTrack = "media.track";
        public const string MediaDisc = "media.disc";
        public const string MediaDate = "media.date";
        public const string MediaYear = "media.year";
        public const string MediaGenre = "media.genre";
        public const string MediaDurationSeconds = "media.durationSeconds";
        public const string MediaAudioCodec = "media.audioCodec";
        public const string MediaVideoCodec = "media.videoCodec";
        public const string MediaWidth = "media.width";
        public const string MediaHeight = "media.height";

        public static readonly string[] ManagedPrefixes =
        [
            "contentMetadata.",
            "image.",
            "media.",
        ];
    }
}
