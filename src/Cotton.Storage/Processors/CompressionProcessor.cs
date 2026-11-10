// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Crypto.Streams;
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

        public Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null, CancellationToken cancellationToken = default)
        {
            DecompressionStream decompressor = new DecompressionStream(stream);
            return Task.FromResult<Stream>(decompressor);
        }

        public Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);

            PipeOptions options = new(
                pool: MemoryPool<byte>.Shared,
                pauseWriterThreshold: 1024 * 1024,
                resumeWriterThreshold: 512 * 1024,
                minimumSegmentSize: 4096,
                useSynchronizationContext: false);
            int level = _compressionLevelProvider.Level;
            Stream result = new TransformReadStream(stream, async (source, destination, token) =>
            {
                await using CompressionStream compressor = new(destination, level: level, leaveOpen: true);
                await source.CopyToAsync(compressor, CompressBufferSize, token).ConfigureAwait(false);
                await compressor.FlushAsync(token).ConfigureAwait(false);
            }, options, cancellationToken: cancellationToken);
            return Task.FromResult(result);
        }

        private class StaticCompressionLevelProvider(int level) : ICompressionLevelProvider
        {
            public int Level { get; } = level;
        }
    }
}
