// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public class AudioPreviewGenerator : IPreviewGenerator
    {
        public string Id => "audio";

        public int Version => 7;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes =>
        [
            "audio/mpeg",
            "audio/mp3",
            "audio/flac",
            "audio/ogg",
            "audio/wav",
            "audio/x-wav",
            "audio/aac",
            "audio/mp4",
            "audio/x-m4a",
            "audio/x-flac",
            "audio/opus",
            "audio/webm",
            "audio/x-aiff",
            "audio/aiff",
        ];

        public Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 150) =>
            MediaPreviewReader.ReadAsync(stream, size, seekToMiddle: false);
    }
}
