// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using EasyExtensions.Models.Enums;
using ZstdSharp;

namespace Cotton.Storage.Processors
{
    public class CompressionProcessor : IStorageProcessor
    {
        public const CompressionAlgorithm Algorithm = CompressionAlgorithm.Zstd;
        public int Priority => 10000;

        public async Task<Stream> ReadAsync(string uid, Stream stream)
        {
            var decompressor = new DecompressionStream(stream);
            var memoryStream = new MemoryStream();
            await decompressor.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public async Task<Stream> WriteAsync(string uid, Stream stream)
        {
            var memoryStream = new MemoryStream();
            using (var compressor = new CompressionStream(memoryStream, leaveOpen: true))
            {
                await stream.CopyToAsync(compressor);
            }
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}
