// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public class VideoPreviewGenerator : IPreviewGenerator
    {
        public string Id => "video";

        public int Version => 5;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes =>
        [
            "video/mp4",
            "video/webm",
            "video/ogg",
            "video/avi",
            "video/mov",
            "video/quicktime",
            "video/x-quicktime",
            "video/mkv",
            "video/msvideo",
            "video/x-msvideo",
            "video/vnd.avi",
            "video/matroska",
            "video/x-matroska",
        ];

        public Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 150) =>
            MediaPreviewReader.ReadAsync(stream, size, seekToMiddle: true);
    }
}
