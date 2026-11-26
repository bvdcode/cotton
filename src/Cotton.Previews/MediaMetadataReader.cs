// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using Mediana;
using Mediana.Containers;
using Mediana.Core;
using Mediana.Core.Metadata;
using Mediana.Core.Timing;
using Mediana.Core.Video;
using Mediana.IO;
using System.Globalization;

namespace Cotton.Previews
{
    public static class MediaMetadataReader
    {
        private static readonly MediaEngine Engine = MediaDecoderRegistry.CreateMetadataEngine();

        public static async Task<MediaMetadataInfo> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Media metadata extraction requires a seekable stream.");
            }

            stream.Position = 0;
            await using RandomAccessStreamByteSource source = new(stream, leaveOpen: true);
            MediaInfo info = await Engine.ProbeAsync(source, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Describe(info, stream.Length);
        }

        internal static MediaMetadataInfo Describe(MediaInfo info, long inputLength)
        {
            ContainerInfo container = info.Container ?? throw new InvalidDataException("Media container metadata is missing.");
            Dictionary<string, string> properties = new(StringComparer.Ordinal);
            MediaMetadataTagCollector tagCollector = new();
            Dictionary<string, string> tags = tagCollector.Collect(container.Tags, container.Dates, MediaMetadataFields.FormatTags);
            double? duration = container.DeclaredDuration is MediaDuration time
                ? (double)time.Value * time.TimeBase.Numerator / time.TimeBase.Denominator : null;
            properties["media.format.format_name"] = container.FormatId;
            if (duration is > 0)
            {
                WriteNumber(properties, "media.format.bit_rate", inputLength * 8.0 / duration.Value);
            }
            WriteTags(properties, "media.format.tags", tags, MediaMetadataFields.LocationTags);

            string? videoCodec = null;
            string? audioCodec = null;
            int? width = null;
            int? height = null;
            for (int index = 0; index < info.Streams.Count; index++)
            {
                EncodedStreamInfo stream = info.Streams[index];
                string key = $"media.streams.{index.ToString(CultureInfo.InvariantCulture)}";
                string codec = GetCodecName(stream.CodecId);
                properties[$"{key}.index"] = stream.StreamIndex.ToString(CultureInfo.InvariantCulture);
                properties[$"{key}.codec_type"] = stream.Kind.ToString().ToLowerInvariant();
                properties[$"{key}.codec_name"] = codec;
                ContainerStreamInfo? track = container.StreamInfo?.GetValueOrDefault(stream.StreamIndex);
                StreamMetadata? metadata = info.StreamMetadata.GetValueOrDefault(stream.StreamIndex);

                WriteTrack(properties, key, track, tagCollector);
                if (stream.Kind == MediaKind.Video)
                {
                    VideoStreamConfiguration? video = (metadata as VideoStreamMetadata)?.Configurations.FirstOrDefault();
                    if (videoCodec is null)
                    {
                        videoCodec = codec;
                        width = video?.Width;
                        height = video?.Height;
                    }
                    WriteVideo(properties, key, stream, video);
                }
                else if (stream.Kind == MediaKind.Audio)
                {
                    audioCodec ??= codec;
                    if (metadata is AudioStreamMetadata audio)
                    {
                        WriteAudio(properties, key, audio);
                    }
                }
            }

            return new MediaMetadataInfo(duration, videoCodec, audioCodec, width, height, tags, properties);
        }

        private static string GetCodecName(string codecId) => codecId switch
        {
            "g711-alaw" => "pcm_alaw",
            "g711-mulaw" => "pcm_mulaw",
            "adpcm-ima-wave" => "adpcm_ima_wav",
            "adpcm-ms" => "adpcm_ms",
            _ => codecId,
        };

        private static void WriteTrack(
            Dictionary<string, string> properties, string key, ContainerStreamInfo? track, MediaMetadataTagCollector collector)
        {
            if (track is null)
            {
                return;
            }
            if (track.AverageFrameRate is FrameRate rate)
            {
                properties[$"{key}.avg_frame_rate"] = FormattableString.Invariant($"{rate.Numerator}/{rate.Denominator}");
            }
            if (track.PacketIndex?.AveragePayloadBitRate is double bitRate)
            {
                WriteNumber(properties, $"{key}.bit_rate", bitRate);
            }
            Dictionary<string, string> tags = collector.Collect(track.Tags, track.Dates, MediaMetadataFields.StreamTags);
            if (track.Language is string language)
            {
                collector.Add(tags, "language", language);
            }
            WriteTags(properties, $"{key}.tags", tags, MediaMetadataFields.StreamTags);
        }

        private static void WriteAudio(Dictionary<string, string> properties, string key, AudioStreamMetadata audio)
        {
            properties[$"{key}.sample_rate"] = audio.SampleRate.ToString(CultureInfo.InvariantCulture);
            properties[$"{key}.channels"] = audio.ChannelCount.ToString(CultureInfo.InvariantCulture);
            if (audio.ChannelOrder is { Count: > 0 } speakers)
            {
                properties[$"{key}.channel_layout"] = string.Join('+', speakers);
            }
            if (audio.CodecProfile is CodecProfileInfo profile)
            {
                properties[$"{key}.profile"] = profile.Name;
            }
        }

        private static void WriteVideo(
            Dictionary<string, string> properties, string key, EncodedStreamInfo stream, VideoStreamConfiguration? video)
        {
            if (stream.DisplayMatrix is VideoDisplayMatrix matrix && matrix != VideoDisplayMatrix.Identity)
            {
                double rotation = -Math.Atan2(matrix.M12, matrix.M11) * 180.0 / Math.PI;
                WriteNumber(properties, $"{key}.side_data_list.0.rotation", rotation);
            }
            SampleAspectRatio? aspectRatio = stream.AspectRatioDescription?.Resolve(video?.SampleAspectRatio) ?? video?.SampleAspectRatio;
            if (aspectRatio is SampleAspectRatio aspect)
            {
                properties[$"{key}.sample_aspect_ratio"] = FormattableString.Invariant($"{aspect.Numerator}:{aspect.Denominator}");
            }
            if (video is null)
            {
                return;
            }

            properties[$"{key}.width"] = video.Width.ToString(CultureInfo.InvariantCulture);
            properties[$"{key}.height"] = video.Height.ToString(CultureInfo.InvariantCulture);
            string sampling = video.ChromaSampling switch
            {
                VideoChromaSampling.Monochrome => "gray",
                VideoChromaSampling.Sampling420 => "yuv420p",
                VideoChromaSampling.Sampling422 => "yuv422p",
                VideoChromaSampling.Sampling444 => "yuv444p",
                _ => throw new ArgumentOutOfRangeException(nameof(video)),
            };
            properties[$"{key}.pix_fmt"] = video.LumaBitDepth == 8
                ? sampling : FormattableString.Invariant($"{sampling}{video.LumaBitDepth}");
            if (video.CodecProfile is CodecProfileInfo profile)
            {
                properties[$"{key}.profile"] = profile.Name;
            }

            VideoColorInfo color = stream.ColorDescription?.Resolve(video.Color) ?? video.Color;
            if (color.Primaries != ColorPrimaries.Unspecified)
            {
                properties[$"{key}.color_primaries"] = color.Primaries.ToString().ToLowerInvariant();
            }
            if (color.Transfer != TransferFunction.Unspecified)
            {
                properties[$"{key}.color_transfer"] = color.Transfer.ToString().ToLowerInvariant();
            }
            if (color.Matrix != MatrixCoefficients.Unspecified)
            {
                properties[$"{key}.color_space"] = color.Matrix.ToString().ToLowerInvariant();
            }
        }

        private static void WriteTags(Dictionary<string, string> properties, string prefix,
            Dictionary<string, string> tags, IReadOnlyCollection<string> keys)
        {
            foreach (string name in keys)
            {
                if (tags.TryGetValue(name, out string? value))
                {
                    FlatMetadataWriter.WriteText(properties, $"{prefix}.{FlatMetadataWriter.EscapeKey(name.ToLowerInvariant())}", value);
                }
            }
        }

        private static void WriteNumber(Dictionary<string, string> properties, string key, double value)
        {
            properties[key] = value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
