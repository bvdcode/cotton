// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions
{
    public interface IStorageProcessor
    {
        int Priority { get; }
        Task<Stream> ReadAsync(string uid, Stream stream);
        Task<Stream> WriteAsync(string uid, Stream stream);
    }
}
