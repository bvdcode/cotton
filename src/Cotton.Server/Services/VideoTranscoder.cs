// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana;
using Mediana.Codecs;
using Mediana.Codecs.Aac;
using Mediana.Codecs.Ac3;
using Mediana.Codecs.Av1;
using Mediana.Codecs.Dts;
using Mediana.Codecs.Flac;
using Mediana.Codecs.H264;
using Mediana.Codecs.Hevc;
using Mediana.Codecs.Jpeg;
using Mediana.Codecs.Mp3;
using Mediana.Codecs.Mpeg4Visual;
using Mediana.Codecs.Opus;
using Mediana.Codecs.Pcm;
using Mediana.Codecs.ProRes;
using Mediana.Codecs.Vorbis;
using Mediana.Codecs.Vp8;
using Mediana.Codecs.Vp9;
using Mediana.Containers.Avi;
using Mediana.Containers.Matroska;
using Mediana.Containers.Mp4;
using Mediana.Containers.MpegTs;
using Mediana.Core;
using Mediana.Core.Memory;
using Mediana.Core.Timing;
using Mediana.Core.Video;
using Mediana.Execution;
using Mediana.IO;
using Mediana.Processing;

namespace Cotton.Server.Services
{
    public class VideoTranscoder
    {
        public const string SegmentContentType = "video/mp2t";

        private const int BufferBudgetBytes = 256 * 1024 * 1024;
        private static readonly MediaDuration TargetDuration = new(HlsManifestBuilder.SegmentDurationSeconds, new TimeBase(1, 1));

        private static readonly IVideoDecoderFactory[] VideoDecoders =
        [
            new H264DecoderFactory(), new HevcDecoderFactory(), new Mpeg4DecoderFactory(),
            new MjpegDecoderFactory(), new Vp8DecoderFactory(), new Vp9DecoderFactory(),
            new Av1DecoderFactory(), new ProResDecoderFactory(),
        ];

        private static readonly IAudioDecoderFactory[] AudioDecoders =
        [
            new AacDecoderFactory(), new PcmDecoderFactory(), new Mp3DecoderFactory(),
            new OpusDecoderFactory(), new VorbisDecoderFactory(), new FlacDecoderFactory(),
            new Ac3DecoderFactory(), new Eac3DecoderFactory(), new DtsDecoderFactory(),
        ];

        private static readonly MediaEngine Engine = BuildEngine();

        internal async Task<HlsVideoPlan> PlanAsync(IRandomAccessByteSource input, CancellationToken cancellationToken)
        {
            MediaBufferAllocator allocator = new(BufferBudgetBytes);
            await using ContainerSession container = await Engine.OpenContainerAsync(
                input, allocator, new MediaOpenOptions(), cancellationToken);

            EncodedStreamInfo video = container.Streams.FirstOrDefault(stream => stream.Kind == MediaKind.Video)
                ?? throw new NotSupportedException("The container has no video track.");
            RequireDecoder(video, VideoDecoders);
            EncodedStreamInfo? audio = container.Streams.FirstOrDefault(stream => stream.Kind == MediaKind.Audio);
            if (audio is not null)
            {
                RequireDecoder(audio, AudioDecoders);
            }

            using VideoSegmentPlan segments = container.PlanVideoSegments(video.StreamIndex, TargetDuration, allocator, cancellationToken);
            return new HlsVideoPlan(video.StreamIndex, audio?.StreamIndex, Array.AsReadOnly(segments.ToArray()));
        }

        internal async Task<byte[]> TranscodeSegmentAsync(
            IRandomAccessByteSource input, HlsVideoPlan plan, int segmentIndex, HlsRendition rendition, CancellationToken cancellationToken)
        {
            HlsRenditionProfile.EncoderPlan profile = HlsRenditionProfile.Plan(rendition);
            MediaTimeRange interval = plan.Segments[segmentIndex];
            H264EncoderFactory encoder = new(new H264EncoderOptions
            {
                Quantizer = profile.Quantizer,
                EntropyCoding = H264EntropyCoding.Cabac,
            });
            List<MediaTrackRequest> tracks =
            [
                new VideoEncodingRequest(plan.VideoStreamIndex, 0, encoder, new VideoPreparationOptions
                {
                    MaximumWidth = profile.MaximumWidth,
                    OutputPixelFormat = VideoPixelFormat.Yuv420P8,
                    RgbConversion = new(),
                }, outputTimeBase: interval.Start.TimeBase, intervalAlignment: VideoIntervalAlignment.CompleteFrames)
                {
                    OverlapPreparation = Environment.ProcessorCount > 1,
                },
            ];
            if (plan.AudioStreamIndex is int audioIndex)
            {
                tracks.Add(new AudioEncodingRequest(audioIndex, 1, new AacEncoderFactory(new AacEncoderOptions
                {
                    BitRate = profile.AudioBitRate,
                }), channels: 2));
            }

            MediaBufferAllocator allocator = new(BufferBudgetBytes);
            using HlsSegmentBuffer output = new();
            await Engine.ProcessAsync(input, output, "mpegts", tracks, allocator,
                new MediaProcessingOptions { Selection = interval }, cancellationToken);
            return output.ToArray();
        }

        private static void RequireDecoder<TFrame>(EncodedStreamInfo stream, IEnumerable<IDecoderFactory<TFrame>> decoders)
            where TFrame : class, IDisposable
        {
            if (!decoders.Any(decoder => decoder.CodecId == stream.CodecId && decoder.GetUnsupportedReason(stream) is null))
            {
                throw new NotSupportedException($"The codec '{stream.CodecId}' is not supported for streaming.");
            }
        }

        private static MediaEngine BuildEngine()
        {
            MediaEngineBuilder builder = new MediaEngineBuilder()
                .AddContainer(new AviReaderFactory())
                .AddContainer(new MatroskaReaderFactory())
                .AddContainer(new Mp4ReaderFactory(new Mp4ReadOptions { TimestampMode = Mp4TimestampMode.LinearMovie }))
                .AddContainerWriter(new MpegTsWriterFactory());

            foreach (IVideoDecoderFactory decoder in VideoDecoders)
            {
                builder.AddVideoDecoder(decoder);
            }

            foreach (IAudioDecoderFactory decoder in AudioDecoders)
            {
                builder.AddAudioDecoder(decoder);
            }

            return builder.Build();
        }
    }
}
