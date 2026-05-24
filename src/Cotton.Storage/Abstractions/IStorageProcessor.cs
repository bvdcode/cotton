// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Pipelines;

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Transforms blob streams as they move between Cotton and the backend.
    /// </summary>
    public interface IStorageProcessor
    {
        /// <summary>
        /// Gets the priority level associated with the current instance.
        /// Lower values indicate higher priority - closer to the beginning of the pipeline (Backend).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Asynchronously reads data associated with the specified unique identifier and writes it to the provided
        /// stream.
        /// </summary>
        /// <param name="uid">The unique identifier of the data to read. Cannot be null or empty.</param>
        /// <param name="stream">The stream to which the data will be written. Must be writable and not null.</param>
        /// <param name="context">Optional metadata shared by processors during a single pipeline operation.</param>
        /// <returns>A task whose result is the transformed readable stream for the next processor.</returns>
        Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null);

        /// <summary>
        /// Asynchronously writes the contents of the specified stream to the resource identified by the given unique
        /// identifier.
        /// </summary>
        /// <param name="uid">The unique identifier of the target resource to which the stream will be written. Cannot be null or empty.</param>
        /// <param name="stream">The stream containing the data to write. Must be readable and positioned at the start of the data to be
        /// written. Cannot be null.</param>
        /// <param name="context">Optional metadata shared by processors during a single pipeline operation.</param>
        /// <returns>A task whose result is the transformed readable stream for the next processor.</returns>
        Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null);
    }
}
