// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Static helpers and constants for computing SHA-256 hashes. All members are thread-safe.
    /// </summary>
    public partial class Hasher
    {
        /// <summary>
        /// Gets the name of the hash algorithm supported by this implementation.
        /// </summary>
        public static string SupportedHashAlgorithm => nameof(SHA256);

        /// <summary>
        /// Gets the hash algorithm name supported by this implementation.
        /// </summary>
        public static HashAlgorithmName SupportedHashAlgorithmName => HashAlgorithmName.SHA256;

        /// <summary>
        /// SHA-256 hash size in bytes.
        /// </summary>
        public const int HashSizeInBytes = 32;

        /// <summary>
        /// SHA-256 hash of empty input, as a lowercase hex string.
        /// </summary>
        public const string ZeroHashHexString = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

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

        /// <summary>
        /// Computes the SHA-256 hash value for the specified span of bytes.
        /// </summary>
        /// <param name="span">The span of bytes to compute the hash for.</param>
        /// <returns>A byte array containing the SHA-256 hash of the input data.</returns>
        public static byte[] HashData(Span<byte> span)
        {
            return SHA256.HashData(span);
        }

        /// <summary>
        /// Computes the SHA-256 hash of the stream asynchronously.
        /// </summary>
        public static async Task<byte[]> HashDataAsync(Stream stream)
        {
            return await SHA256.HashDataAsync(stream);
        }

        /// <summary>
        /// Converts a hash to its lowercase hex string representation.
        /// </summary>
        public static string ToHexStringHash(byte[] hash)
        {
            return Convert.ToHexStringLower(hash);
        }

        /// <summary>
        /// Parses a hex string into a hash, validating its length and characters.
        /// </summary>
        /// <exception cref="ArgumentException">The string is empty, has an odd or wrong length, or contains non-hex characters.</exception>
        public static byte[] FromHexStringHash(string hexString)
        {
            if (string.IsNullOrWhiteSpace(hexString))
            {
                throw new ArgumentException("Hex string cannot be null or empty.", nameof(hexString));
            }
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have an even length.", nameof(hexString));
            }
            if (hexString.Length / 2 != HashSizeInBytes)
            {
                throw new ArgumentException($"Hex string must represent a hash of {HashSizeInBytes} bytes.", nameof(hexString));
            }
            if (!HexStringRegex().IsMatch(hexString))
            {
                throw new ArgumentException("Hex string contains invalid characters.", nameof(hexString));
            }
            return Convert.FromHexString(hexString);
        }

        /// <summary>
        /// Returns true if the string is a valid hex-encoded SHA-256 hash.
        /// </summary>
        public static bool IsValidHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }
            if (hash.Length != HashSizeInBytes * 2)
            {
                return false;
            }
            if (!HexStringRegex().IsMatch(hash))
            {
                return false;
            }
            return true;
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\A\b[0-9a-fA-F]+\b\Z")]
        private static partial System.Text.RegularExpressions.Regex HexStringRegex();
    }
}
