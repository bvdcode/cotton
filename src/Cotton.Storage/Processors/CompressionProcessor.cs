// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;

namespace Cotton.Storage.Processors
{
    public class CompressionProcessor : IStorageProcessor
    {
        public int Priority => 1000;

        public Task<Stream> ReadAsync(string uid, Stream stream)
        {
            return Task.FromResult(stream);
            // return Task.FromResult<Stream>(new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true));
        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {
            return Task.FromResult(stream);
            // return Task.FromResult<Stream>(new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true));
        }
    }
}
