// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Previews;
using Cotton.Previews.Http;
using System.Globalization;

namespace Cotton.Server.Services.FileMetadata
{
    /// <summary>
    /// Extracts audio and video metadata through ffprobe.
    /// </summary>
    internal class MediaFileContentMetadataExtractor(ILogger<MediaFileContentMetadataExtractor> _logger) : IFileContentMetadataExtractor
    {
        private static readonly string[] TitleAliases = ["title"];
        private static readonly string[] ArtistAliases = ["artist", "album_artist", "album artist", "albumartist", "composer", "performer"];
        private static readonly string[] AlbumAliases = ["album"];
        private static readonly string[] AlbumArtistAliases = ["album_artist", "album artist", "albumartist"];
        private static readonly string[] TrackAliases = ["track", "tracknumber", "track_number"];
        private static readonly string[] DiscAliases = ["disc", "discnumber", "disc_number"];
        private static readonly string[] DateAliases = ["date", "creation_time"];
        private static readonly string[] YearAliases = ["year"];
        private static readonly string[] GenreAliases = ["genre"];

        /// <inheritdoc />
        public bool Supports(string contentType) =>
            contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public async Task<IReadOnlyDictionary<string, string>> ExtractAsync(
            Stream stream,
            string contentType,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Media metadata extraction requires a seekable stream.");
            }

            stream.Position = 0;

            await using RangeStreamServer server = new(stream, _logger);
            MediaMetadataInfo? metadata = await FfmpegBinary.TryGetMediaMetadataAsync(
                server.Url,
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            if (metadata is null)
            {
                throw new InvalidDataException("ffprobe did not produce valid media metadata.");
            }

            Dictionary<string, string> result = new(StringComparer.Ordinal);
            AddNumber(result, FileContentMetadataKeys.MediaDurationSeconds, metadata.DurationSeconds);
            AddValue(result, FileContentMetadataKeys.MediaAudioCodec, metadata.AudioCodec);
            AddValue(result, FileContentMetadataKeys.MediaVideoCodec, metadata.VideoCodec);
            AddInt(result, FileContentMetadataKeys.MediaWidth, metadata.Width);
            AddInt(result, FileContentMetadataKeys.MediaHeight, metadata.Height);
            AddTag(result, FileContentMetadataKeys.MediaTitle, metadata.Tags, TitleAliases);
            AddTag(result, FileContentMetadataKeys.MediaArtist, metadata.Tags, ArtistAliases);
            AddTag(result, FileContentMetadataKeys.MediaAlbum, metadata.Tags, AlbumAliases);
            AddTag(result, FileContentMetadataKeys.MediaAlbumArtist, metadata.Tags, AlbumArtistAliases);
            AddTag(result, FileContentMetadataKeys.MediaTrack, metadata.Tags, TrackAliases);
            AddTag(result, FileContentMetadataKeys.MediaDisc, metadata.Tags, DiscAliases);
            AddTag(result, FileContentMetadataKeys.MediaDate, metadata.Tags, DateAliases);
            AddTag(result, FileContentMetadataKeys.MediaYear, metadata.Tags, YearAliases);
            AddTag(result, FileContentMetadataKeys.MediaGenre, metadata.Tags, GenreAliases);

            return result;
        }

        private static void AddTag(
            Dictionary<string, string> target,
            string targetKey,
            IReadOnlyDictionary<string, string> tags,
            IReadOnlyCollection<string> aliases)
        {
            foreach (string alias in aliases)
            {
                if (tags.TryGetValue(alias, out string? value))
                {
                    AddValue(target, targetKey, value);
                    return;
                }
            }
        }

        private static void AddNumber(Dictionary<string, string> target, string key, double? value)
        {
            if (value is null || value <= 0)
            {
                return;
            }

            target[key] = value.Value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void AddInt(Dictionary<string, string> target, string key, int? value)
        {
            if (value is null || value <= 0)
            {
                return;
            }

            target[key] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static void AddValue(Dictionary<string, string> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[key] = value.Trim();
            }
        }
    }
}
