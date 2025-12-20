// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Provides static methods and constants for computing SHA-256 hashes of data.
    /// </summary>
    /// <remarks>The Hasher class offers a simple interface for generating SHA-256 hashes from byte arrays or
    /// streams. All members are thread-safe and can be used concurrently across multiple threads.</remarks>
    public class Hasher
    {
        /// <summary>
        /// Gets the name of the hash algorithm supported by this implementation.
        /// </summary>
        public static string SupportedHashAlgorithm => nameof(SHA256);

        /// <summary>
        /// Specifies the size, in bytes, of the hash output.
        /// </summary>
        public const int HashSizeInBytes = 32;

        /// <summary>
        /// Computes the SHA-256 hash value for the specified byte array.
        /// </summary>
        /// <param name="content">The input data to compute the hash for. Cannot be null.</param>
        /// <returns>A byte array containing the SHA-256 hash of the input data.</returns>
        public static byte[] HashData(byte[] content)
        {
            return SHA256.HashData(content);
        }

        /// <summary>
        /// Computes the SHA-256 hash value for the data in the specified stream.
        /// </summary>
        /// <remarks>The method reads from the current position of the stream to the end. The position of
        /// the stream is not reset after the operation completes.</remarks>
        /// <param name="input">The input stream containing the data to hash. The stream must be readable and seekable.</param>
        /// <returns>A byte array containing the SHA-256 hash of the input data.</returns>
        public static byte[] HashData(Stream input)
        {
            return SHA256.HashData(input);
        }

        public static async Task<byte[]> HashDataAsync(Stream stream)
        {
            return await SHA256.HashDataAsync(stream);
        }
    }
}
