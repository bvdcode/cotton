// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using Mediana;
using Mediana.Codecs;
using Mediana.Codecs.Aac;
using Mediana.Codecs.Ac3;
using Mediana.Codecs.Alac;
using Mediana.Codecs.Av1;
using Mediana.Codecs.Dts;
using Mediana.Codecs.Dv;
using Mediana.Codecs.Flac;
using Mediana.Codecs.H264;
using Mediana.Codecs.Hevc;
using Mediana.Codecs.Jpeg;
using Mediana.Codecs.Mp3;
using Mediana.Codecs.Mpeg4Visual;
using Mediana.Codecs.Opus;
using Mediana.Codecs.Pcm;
using Mediana.Codecs.ProRes;
using Mediana.Codecs.Theora;
using Mediana.Codecs.Vorbis;
using Mediana.Codecs.Vp8;
using Mediana.Codecs.Vp9;
using Mediana.Containers.Ac3;
using Mediana.Containers.Adts;
using Mediana.Containers.Aiff;
using Mediana.Containers.Avi;
using Mediana.Containers.Flac;
using Mediana.Containers.Matroska;
using Mediana.Containers.Mp3;
using Mediana.Containers.Mp4;
using Mediana.Containers.Ogg;
using Mediana.Containers.Wave;
using Mediana.Core;
using Mediana.Formats.Audio;
using Mediana.Formats.Video;

namespace Cotton.Previews
{
    public static class MediaDecoderRegistry
    {
        private static readonly WaveCompressedAudioCodec[] WaveCodecs =
        [
            new(WaveCompressedAudioEncoding.ALaw), new(WaveCompressedAudioEncoding.MuLaw),
            new(WaveCompressedAudioEncoding.ImaAdpcm), new(WaveCompressedAudioEncoding.MicrosoftAdpcm),
        ];

        private static readonly IVideoDecoderFactory[] VideoDecoders =
        [
            new H264DecoderFactory(), new HevcDecoderFactory(), new Mpeg4DecoderFactory(),
            new MicrosoftMpeg4DecoderFactory(MicrosoftMpeg4Configuration.Version3CodecId),
            new MicrosoftMpeg4DecoderFactory(MicrosoftMpeg4Configuration.Wmv2CodecId),
            new MjpegDecoderFactory(), new Vp8DecoderFactory(), new Vp9DecoderFactory(),
            new Av1DecoderFactory(), new ProResDecoderFactory(), new TheoraDecoderFactory(), new DvDecoderFactory(),
        ];

        private static readonly IAudioDecoderFactory[] AudioDecoders =
        [
            new AacDecoderFactory(), new PcmDecoderFactory(), new AiffPcmDecoderFactory(), new Mp3DecoderFactory(),
            new OpusDecoderFactory(), new VorbisDecoderFactory(), new FlacDecoderFactory(), new AlacDecoderFactory(),
            new Ac3DecoderFactory(), new Eac3DecoderFactory(), new DtsDecoderFactory(), .. WaveCodecs,
        ];

        public static MediaEngineBuilder CreateBuilder()
        {
            MediaEngineBuilder builder = new MediaEngineBuilder()
                .AddContainer(new AviReaderFactory())
                .AddContainer(new MatroskaReaderFactory())
                .AddContainer(new Mp4ReaderFactory(new Mp4ReadOptions { TimestampMode = Mp4TimestampMode.LinearMovie }))
                .AddContainer(new OggReaderFactory())
                .AddContainer(new WaveReaderFactory())
                .AddContainer(new AiffReaderFactory())
                .AddContainer(new Mp3ReaderFactory())
                .AddContainer(new FlacReaderFactory())
                .AddContainer(new AdtsReaderFactory())
                .AddContainer(new Ac3ReaderFactory());

            foreach (IVideoDecoderFactory decoder in VideoDecoders)
            {
                builder.AddVideoDecoder(decoder);
            }
            foreach (IAudioDecoderFactory decoder in AudioDecoders)
            {
                builder.AddAudioDecoder(decoder);
            }
            return builder;
        }

        public static bool SupportsDecoding(EncodedStreamInfo stream) => stream.Kind switch
        {
            MediaKind.Video => Supports(stream, VideoDecoders),
            MediaKind.Audio => Supports(stream, AudioDecoders),
            MediaKind.Subtitle or MediaKind.Data => false,
            _ => throw new ArgumentOutOfRangeException(nameof(stream)),
        };

        private static bool Supports<TFrame>(EncodedStreamInfo stream, IEnumerable<IDecoderFactory<TFrame>> decoders)
            where TFrame : class, IDisposable
        {
            return decoders.Any(decoder => decoder.CodecId == stream.CodecId && decoder.GetUnsupportedReason(stream) is null);
        }
    }
}
