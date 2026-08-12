// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Pipelines;

namespace Cotton.Storage.Abstractions
{
    public interface IStorageProcessor
    {
        int Priority { get; }

        Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null);

        Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null);
    }
}
