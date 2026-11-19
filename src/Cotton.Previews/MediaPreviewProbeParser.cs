// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text.Json;

namespace Cotton.Previews
{
    internal static class MediaPreviewProbeParser
    {
        public static MediaPreviewProbeInfo Parse(string raw)
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            int? videoStreamIndex = null;
            double? videoDurationSeconds = null;
            bool hasAudio = false;
            if (document.RootElement.TryGetProperty("streams", out JsonElement streams))
            {
                foreach (JsonElement stream in streams.EnumerateArray())
                {
                    if (!stream.TryGetProperty("codec_type", out JsonElement codecType))
                    {
                        continue;
                    }

                    switch (codecType.GetString())
                    {
                        case "audio":
                            hasAudio = true;
                            break;
                        case "video" when videoStreamIndex is null && !IsCoverArt(stream):
                            videoStreamIndex = stream.GetProperty("index").GetInt32();
                            if (stream.TryGetProperty("duration", out JsonElement duration)
                                && double.TryParse(duration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                                && double.IsFinite(seconds) && seconds > 0)
                            {
                                videoDurationSeconds = seconds;
                            }
                            break;
                    }
                }
            }
            return new MediaPreviewProbeInfo(videoStreamIndex, videoDurationSeconds, hasAudio);
        }

        private static bool IsCoverArt(JsonElement stream)
        {
            return stream.TryGetProperty("disposition", out JsonElement disposition)
                && disposition.TryGetProperty("attached_pic", out JsonElement attachedPicture)
                && attachedPicture.GetInt32() != 0;
        }
    }
}
