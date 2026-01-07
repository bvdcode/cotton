// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Pipelines;

namespace Cotton.Storage.Abstractions
{
    public interface IStoragePipeline
    {
        Task<bool> ExistsAsync(string uid);
        Task<Stream> ReadAsync(string uid, PipelineContext? context);
        Task WriteAsync(string uid, Stream stream, PipelineContext? context);
    }
}
