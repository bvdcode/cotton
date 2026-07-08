// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Crypto
{
    /// <summary>
    /// Encrypts and decrypts streams with Cotton's authenticated stream container format.
    /// </summary>
    public interface IStreamCipher
    {
        /// <summary>
        /// Encrypts the input stream into the output stream.
        /// </summary>
        Task EncryptAsync(
            Stream input,
            Stream output,
            int chunkSize = AesGcmStreamCipher.DefaultChunkSize,
            bool leaveInputOpen = true,
            bool leaveOutputOpen = true,
            CancellationToken ct = default);

        /// <summary>
        /// Decrypts the input stream into the output stream.
        /// </summary>
        Task DecryptAsync(
            Stream input,
            Stream output,
            bool leaveInputOpen = true,
            bool leaveOutputOpen = true,
            CancellationToken ct = default);

        /// <summary>
        /// Returns a readable encrypted stream for the input stream.
        /// </summary>
        Task<Stream> EncryptAsync(
            Stream input,
            int chunkSize = AesGcmStreamCipher.DefaultChunkSize,
            bool leaveOpen = false,
            CancellationToken ct = default);

        /// <summary>
        /// Returns a readable decrypted stream for the input stream.
        /// </summary>
        Task<Stream> DecryptAsync(
            Stream input,
            bool leaveOpen = false,
            CancellationToken ct = default);
    }
}
