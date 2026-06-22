// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Resolves video playback.
    /// </summary>
    public static class VideoPlaybackResolver
    {
        private static readonly HashSet<string> BrowserNativeVideoTypes = new(StringComparer.Ordinal)
        {
            "video/mp4",
            "video/webm",
            "video/ogg",
            "video/quicktime",
        };

        /// <summary>
        /// Indicates whether browser native video.
        /// </summary>
        public static bool IsBrowserNativeVideo(string? contentType) =>
            contentType is not null && BrowserNativeVideoTypes.Contains(contentType);

        /// <summary>
        /// Resolves value.
        /// </summary>
        public static VideoPlaybackMode Resolve(string? contentType, bool hasPreview)
        {
            if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("video/", StringComparison.Ordinal))
            {
                return VideoPlaybackMode.None;
            }

            if (IsBrowserNativeVideo(contentType))
            {
                return VideoPlaybackMode.Native;
            }

            return hasPreview ? VideoPlaybackMode.Transcode : VideoPlaybackMode.Unsupported;
        }

        /// <summary>
        /// Indicates whether the file should use HLS transcoding instead of native playback.
        /// </summary>
        public static bool RequiresTranscoding(string? contentType, bool hasPreview) =>
            Resolve(contentType, hasPreview) == VideoPlaybackMode.Transcode;
    }
}
