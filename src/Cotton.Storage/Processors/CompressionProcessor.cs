// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Models.Enums;
using System.Buffers;
using System.IO.Pipelines;
using ZstdSharp;

namespace Cotton.Storage.Processors
{
    public class CompressionProcessor(
        ICompressionLevelProvider _compressionLevelProvider) : IStorageProcessor
    {
        public const int DefaultCompressionLevel = 1;

        public static readonly int MinCompressionLevel = Compressor.MinCompressionLevel;

        public static readonly int MaxCompressionLevel = Compressor.MaxCompressionLevel;

        public const CompressionAlgorithm Algorithm = CompressionAlgorithm.Zstd;

        public int Priority => 10000;
        private const int CompressBufferSize = 1 * 1024 * 1024;

        public CompressionProcessor()
            : this(new StaticCompressionLevelProvider(DefaultCompressionLevel))
        {
        }

        public static void ThrowIfInvalidLevel(int level)
        {
            if (level < MinCompressionLevel || level > MaxCompressionLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(level),
                    level,
                    $"Compression level must be between {MinCompressionLevel} and {MaxCompressionLevel}.");
            }
        }

        public Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            DecompressionStream decompressor = new DecompressionStream(stream);
            return Task.FromResult<Stream>(decompressor);
        }

        public Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            ArgumentNullException.ThrowIfNull(stream);

            Pipe pipe = new Pipe(new PipeOptions(
                pool: MemoryPool<byte>.Shared,
                readerScheduler: null,
                writerScheduler: null,
                pauseWriterThreshold: 1024 * 1024 * 1,
                resumeWriterThreshold: 512 * 1024,
                minimumSegmentSize: 4096,
                useSynchronizationContext: false));

            Stream readerStream = pipe.Reader.AsStream(leaveOpen: false);
            _ = Task.Run(async () =>
            {
                try
                {
                    await using Stream writerStream = pipe.Writer.AsStream(leaveOpen: true);
                    await using (CompressionStream compressor = new CompressionStream(
                        writerStream,
                        level: _compressionLevelProvider.Level,
                        leaveOpen: true))
                    {
                        await stream.CopyToAsync(compressor, CompressBufferSize).ConfigureAwait(false);
                        await compressor.FlushAsync().ConfigureAwait(false);
                    }

                    await pipe.Writer.CompleteAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException oce)
                {
                    await pipe.Writer.CompleteAsync(oce).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await pipe.Writer.CompleteAsync(ex).ConfigureAwait(false);
                }
                finally
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
            });

            return Task.FromResult<Stream>(readerStream);
        }

        private class StaticCompressionLevelProvider(int level) : ICompressionLevelProvider
        {
            public int Level { get; } = level;
        }
    }
}
