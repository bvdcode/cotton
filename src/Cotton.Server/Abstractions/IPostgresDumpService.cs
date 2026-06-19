// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Abstractions
{
    /// <summary>
    /// Defines the postgres dump service contract used by the server runtime.
    /// </summary>
    public interface IPostgresDumpService
    {
        /// <summary>
        /// Dumps the database to the specified file.
        /// </summary>
        Task DumpToFileAsync(string outputFilePath, CancellationToken cancellationToken = default);
        /// <summary>
        /// Restores from file.
        /// </summary>
        Task RestoreFromFileAsync(string inputFilePath, CancellationToken cancellationToken = default);
    }
}
