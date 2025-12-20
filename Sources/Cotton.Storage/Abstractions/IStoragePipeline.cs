// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions
{
    public interface IStoragePipeline
    {
        Task<Stream> ReadAsync(string uid);
        Task WriteAsync(string uid, Stream stream);
    }
}
