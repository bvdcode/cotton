// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using ZstdSharp;
using Cotton.Storage.Abstractions;
using EasyExtensions.Models.Enums;

namespace Cotton.Storage.Processors
{
    public class CompressionProcessor : IStorageProcessor
    {
        public const CompressionAlgorithm Algorithm = CompressionAlgorithm.Zstd;
        public int Priority => 10000;

        public Task<Stream> ReadAsync(string uid, Stream stream)
        {

        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {

        }
    }
}
