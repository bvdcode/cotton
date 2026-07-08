// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.Crypto
{
    /// <summary>
    /// Provides byte-array and string helpers for Cotton stream ciphers.
    /// </summary>
    public static class StreamCipherExtensions
    {
        /// <summary>
        /// Encrypts the specified bytes with the stream cipher.
        /// </summary>
        public static byte[] Encrypt(
            this IStreamCipher streamCipher,
            byte[] plainBytes,
            int chunkSize = AesGcmStreamCipher.DefaultChunkSize)
        {
            return EncryptAsync(streamCipher, plainBytes, chunkSize).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Decrypts the specified bytes with the stream cipher.
        /// </summary>
        public static byte[] Decrypt(this IStreamCipher streamCipher, byte[] cipherBytes)
        {
            return DecryptAsync(streamCipher, cipherBytes).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Encrypts the specified UTF-8 text with the stream cipher.
        /// </summary>
        public static byte[] EncryptString(
            this IStreamCipher streamCipher,
            string plainText,
            int chunkSize = AesGcmStreamCipher.DefaultChunkSize)
        {
            ArgumentNullException.ThrowIfNull(plainText);

            return Encrypt(streamCipher, Encoding.UTF8.GetBytes(plainText), chunkSize);
        }

        /// <summary>
        /// Decrypts the specified bytes and decodes the result as UTF-8 text.
        /// </summary>
        public static string DecryptString(this IStreamCipher streamCipher, byte[] cipherBytes)
        {
            byte[] plainBytes = Decrypt(streamCipher, cipherBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// Encrypts the specified bytes with the stream cipher.
        /// </summary>
        public static async Task<byte[]> EncryptAsync(
            this IStreamCipher streamCipher,
            byte[] plainBytes,
            int chunkSize = AesGcmStreamCipher.DefaultChunkSize,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(streamCipher);
            ArgumentNullException.ThrowIfNull(plainBytes);

            await using var input = new MemoryStream(plainBytes, writable: false);
            await using var output = new MemoryStream();
            await streamCipher.EncryptAsync(input, output, chunkSize, true, true, cancellationToken)
                .ConfigureAwait(false);
            return output.ToArray();
        }

        /// <summary>
        /// Decrypts the specified bytes with the stream cipher.
        /// </summary>
        public static async Task<byte[]> DecryptAsync(
            this IStreamCipher streamCipher,
            byte[] cipherBytes,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(streamCipher);
            ArgumentNullException.ThrowIfNull(cipherBytes);

            await using var input = new MemoryStream(cipherBytes, writable: false);
            await using var output = new MemoryStream();
            await streamCipher.DecryptAsync(input, output, true, true, cancellationToken)
                .ConfigureAwait(false);
            return output.ToArray();
        }

        /// <summary>
        /// Encrypts the specified UTF-8 text with the stream cipher.
        /// </summary>
        public static async Task<byte[]> EncryptStringAsync(
            this IStreamCipher streamCipher,
            string plainText,
            int chunkSize = AesGcmStreamCipher.DefaultChunkSize,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plainText);

            return await EncryptAsync(
                    streamCipher,
                    Encoding.UTF8.GetBytes(plainText),
                    chunkSize,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Decrypts the specified bytes and decodes the result as UTF-8 text.
        /// </summary>
        public static async Task<string> DecryptStringAsync(
            this IStreamCipher streamCipher,
            byte[] cipherBytes,
            CancellationToken cancellationToken = default)
        {
            byte[] plainBytes = await DecryptAsync(streamCipher, cipherBytes, cancellationToken)
                .ConfigureAwait(false);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
