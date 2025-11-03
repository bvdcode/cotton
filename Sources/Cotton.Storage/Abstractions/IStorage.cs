using System;
using System.Collections.Generic;
using System.Text;

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Defines methods for reading and writing binary data to a storage system using unique identifiers.
    /// </summary>
    /// <remarks>Implementations of this interface provide mechanisms to retrieve and store data streams
    /// associated with unique identifiers. The interface is intended for use with storage backends that support blob or
    /// file-based operations. Thread safety and performance characteristics depend on the specific
    /// implementation.</remarks>
    public interface IStorage
    {
        /// <summary>
        /// Retrieves a stream containing the data for the specified blobs.
        /// </summary>
        /// <param name="uids">An array of unique identifiers representing the blobs to retrieve. Each identifier must correspond to an
        /// existing blob.</param>
        /// <returns>A stream containing the combined data of the specified blobs. The caller is responsible for disposing the
        /// returned stream.</returns>
        public Stream GetBlobStream(string[] uids);

        /// <summary>
        /// Asynchronously writes the contents of the specified stream to a file identified by the given unique
        /// identifier.
        /// </summary>
        /// <param name="uid">The unique identifier of the file to which the stream will be written. Cannot be null or empty.</param>
        /// <param name="stream">The stream containing the data to write to the file. The stream must be readable and positioned at the start
        /// of the data to write.</param>
        /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        public Task WriteFileAsync(string uid, Stream stream, CancellationToken ct = default);
    }
}
