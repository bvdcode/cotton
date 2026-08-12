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
        public static string SupportedHashAlgorithm => nameof(SHA256);

        public static HashAlgorithmName SupportedHashAlgorithmName => HashAlgorithmName.SHA256;

        public const int HashSizeInBytes = 32;

        public const string ZeroHashHexString = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        public static byte[] HashData(byte[] content)
        {
            return SHA256.HashData(content);
        }

        public static byte[] HashData(Span<byte> span)
        {
            return SHA256.HashData(span);
        }

        public static async Task<byte[]> HashDataAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            return await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        public static string ToHexStringHash(byte[] hash)
        {
            return Convert.ToHexStringLower(hash);
        }

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
