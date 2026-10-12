// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.DatabaseBackup;

namespace Cotton.Server.Abstractions
{
    public interface IPostgresDumpService
    {
        Task DumpToFileAsync(string outputFilePath, CancellationToken cancellationToken = default);

        Task<PostgresRestoreResult> RestoreFromFileAsync(string inputFilePath, CancellationToken cancellationToken = default);
    }
}
