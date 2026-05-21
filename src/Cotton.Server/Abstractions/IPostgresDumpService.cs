// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Abstractions
{
    public interface IPostgresDumpService
    {
        Task DumpToFileAsync(string outputFilePath, CancellationToken cancellationToken = default);
        Task RestoreFromFileAsync(string inputFilePath, CancellationToken cancellationToken = default);
    }
}
