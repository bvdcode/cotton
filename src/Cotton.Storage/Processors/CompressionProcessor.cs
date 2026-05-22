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
    /// <summary>
    /// Storage processor that compresses blobs with Zstandard before they reach the backend.
    /// </summary>
    public class CompressionProcessor : IStorageProcessor
    {
        /// <summary>Default Zstandard compression level used for stored blobs.</summary>
        public const int CompressionLevel = 3;
        /// <summary>Compression algorithm used by this processor.</summary>
        public const CompressionAlgorithm Algorithm = CompressionAlgorithm.Zstd;
        /// <inheritdoc />
        public int Priority => 10000;
        private const int CompressBufferSize = 1 * 1024 * 1024;

        /// <inheritdoc />
        public Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            var decompressor = new DecompressionStream(stream);
            return Task.FromResult<Stream>(decompressor);
        }

        /// <inheritdoc />
        public Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            ArgumentNullException.ThrowIfNull(stream);

            var pipe = new Pipe(new PipeOptions(
                pool: MemoryPool<byte>.Shared,
                readerScheduler: null,
                writerScheduler: null,
                pauseWriterThreshold: 1024 * 1024 * 1,
                resumeWriterThreshold: 512 * 1024,
                minimumSegmentSize: 4096,
                useSynchronizationContext: false));

            var readerStream = pipe.Reader.AsStream(leaveOpen: false);
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var writerStream = pipe.Writer.AsStream(leaveOpen: true);
                    await using (var compressor = new CompressionStream(
                        writerStream,
                        level: CompressionLevel,
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
                    stream.Dispose();
                }
            });

            return Task.FromResult<Stream>(readerStream);
        }
    }
}
