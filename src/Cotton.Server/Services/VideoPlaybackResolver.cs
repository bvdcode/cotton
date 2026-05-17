// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public enum VideoPlaybackMode
    {
        None,
        Native,
        Transcode,
        Unsupported,
    }

    public static class VideoPlaybackResolver
    {
        private static readonly HashSet<string> BrowserNativeVideoTypes = new(StringComparer.Ordinal)
        {
            "video/mp4",
            "video/webm",
            "video/ogg",
            "video/quicktime",
        };

        public static bool IsBrowserNativeVideo(string? contentType) =>
            contentType is not null && BrowserNativeVideoTypes.Contains(contentType);

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

        public static bool RequiresTranscoding(string? contentType, bool hasPreview) =>
            Resolve(contentType, hasPreview) == VideoPlaybackMode.Transcode;
    }
}
