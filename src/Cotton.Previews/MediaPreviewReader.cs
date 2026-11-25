// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using Mediana;
using Mediana.Codecs.Bmp;
using Mediana.Codecs.Gif;
using Mediana.Codecs.Jpeg;
using Mediana.Codecs.Png;
using Mediana.Codecs.Tiff;
using Mediana.Codecs.Webp;
using Mediana.Core;
using Mediana.Core.Memory;
using Mediana.Core.Timing;
using Mediana.Core.Video;
using Mediana.IO;
using Mediana.Processing;
using SixLabors.ImageSharp;

namespace Cotton.Previews
{
    internal static class MediaPreviewReader
    {
        private const int BufferBudgetBytes = 128 * 1024 * 1024;
        private static readonly TimeSpan PreviewTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan WaveformTimeout = TimeSpan.FromSeconds(120);
        private static readonly ContainerImageSearchOptions AttachmentsFirst =
            new([ContainerImageSource.Attachment, ContainerImageSource.Picture]);
        private static readonly ContainerImageSearchOptions PicturesFirst =
            new([ContainerImageSource.Picture, ContainerImageSource.Attachment]);

        private static readonly MediaEngine Engine = MediaDecoderRegistry.CreateBuilder()
            .AddImageDecoder(new JpegImageDecoder())
            .AddImageDecoder(new PngImageDecoder())
            .AddImageDecoder(new BmpImageDecoder())
            .AddImageDecoder(new GifImageDecoder())
            .AddImageDecoder(new TiffImageDecoder())
            .AddImageDecoder(new WebpImageDecoder())
            .Build();

        public static async Task<byte[]> ReadAsync(Stream stream, int size, bool seekToMiddle)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
            if (!stream.CanSeek)
            {
                throw new InvalidOperationException("Media preview generation requires a seekable stream.");
            }

            stream.Position = 0;
            await using RandomAccessStreamByteSource source = new(stream, leaveOpen: true);
            MediaBufferAllocator allocator = new(BufferBudgetBytes);
            using CancellationTokenSource preview = new(PreviewTimeout);
            EncodedStreamInfo? video;
            EncodedStreamInfo? audio;
            MediaTimestamp? target = null;

            await using (ContainerSession session = await Engine.OpenContainerAsync(
                source, allocator, new MediaOpenOptions(), preview.Token).ConfigureAwait(false))
            {
                using ContainerImage? cover = await Engine.ReadContainerImageAsync(
                    session, seekToMiddle ? AttachmentsFirst : PicturesFirst, preview.Token).ConfigureAwait(false);
                if (cover is not null)
                {
                    using MemoryStream coverStream = new(cover.Data.ToArray(), writable: false);
                    return await new ImagePreviewGenerator().GeneratePreviewWebPAsync(coverStream, size).ConfigureAwait(false);
                }

                video = session.Streams.FirstOrDefault(stream => stream.Kind == MediaKind.Video);
                audio = session.Streams.FirstOrDefault(stream => stream.Kind == MediaKind.Audio);
                if (seekToMiddle && video is not null)
                {
                    MediaDuration? duration = session.Info.StreamInfo?.GetValueOrDefault(video.StreamIndex)?.DeclaredDuration
                        ?? video.CodedDuration ?? session.Info.DeclaredDuration;
                    if (duration is MediaDuration length && length.Value > (double)length.TimeBase.Denominator / length.TimeBase.Numerator)
                    {
                        target = new MediaTimestamp(length.Value / 2, length.TimeBase);
                    }
                }
            }

            if (video is not null)
            {
                using MemoryStream output = new();
                await Engine.ExportPreviewAsync(source, output, new MediaPreviewWebpEncoder(size), allocator, new VisualExportOptions
                {
                    Input = new VisualOpenOptions { StreamIndex = video.StreamIndex, Request = new() { Time = target } },
                    MaximumWidth = size,
                    MaximumHeight = size,
                    ColorResolver = PreviewColorPolicy.Resolve,
                    ToneMapping = new(),
                    InterlacedField = VideoFieldSelection.Leading,
                    UnspecifiedSampleAspectRatio = SampleAspectRatio.Square,
                }, preview.Token).ConfigureAwait(false);
                return output.ToArray();
            }

            if (audio is null)
            {
                throw new InvalidDataException("Media contains no video or audio streams for preview generation.");
            }

            using CancellationTokenSource waveform = new(WaveformTimeout);
            await using AudioSession audioSession = await Engine.OpenAudioAsync(
                source, audio.StreamIndex, cancellationToken: waveform.Token).ConfigureAwait(false);
            return await AudioWaveformPreview.RenderAsync(audioSession, allocator, size, waveform.Token).ConfigureAwait(false);
        }
    }
}
