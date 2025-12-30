// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions
{
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
        /// <returns>A task that represents the asynchronous read operation. The task result is the stream containing the read
        /// data.</returns>
        Task<Stream> ReadAsync(string uid, Stream stream);

        /// <summary>
        /// Asynchronously writes the contents of the specified stream to the resource identified by the given unique
        /// identifier.
        /// </summary>
        /// <param name="uid">The unique identifier of the target resource to which the stream will be written. Cannot be null or empty.</param>
        /// <param name="stream">The stream containing the data to write. Must be readable and positioned at the start of the data to be
        /// written. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous write operation. The task result contains a stream referencing the
        /// written resource.</returns>
        Task<Stream> WriteAsync(string uid, Stream stream);
    }
}
