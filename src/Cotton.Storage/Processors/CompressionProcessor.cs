// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Models.Enums;
using ZstdSharp;

namespace Cotton.Storage.Processors
{
    public class CompressionProcessor : IStorageProcessor
    {
        public const CompressionAlgorithm Algorithm = CompressionAlgorithm.Zstd;
        public int Priority => 10000;

        public Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            var decompressor = new DecompressionStream(stream);
            return Task.FromResult<Stream>(decompressor);
        }

        public async Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            var memoryStream = new MemoryStream();
            using (var compressor = new CompressionStream(memoryStream, level: 3, leaveOpen: true))
            {
                await stream.CopyToAsync(compressor);
            }
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}
