// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Crypto
{
    /// <summary>
    /// Key derivation using HKDF (RFC 5869) over HMAC-SHA256.
    /// Provides deterministic subkeys from a master key and context info (purpose), with optional salt.
    /// </summary>
    public static class KeyDerivation
    {
        private const int MaxOutputLength = 255 * HMACSHA256.HashSizeInBytes;

        public static byte[] DeriveSubkey(
            ReadOnlySpan<byte> masterKey,
            ReadOnlySpan<byte> info,
            int lengthBytes,
            ReadOnlySpan<byte> salt = default)
        {
            if (lengthBytes == 0)
            {
                return [];
            }

            ArgumentOutOfRangeException.ThrowIfNegative(lengthBytes);
            if (lengthBytes > MaxOutputLength)
            {
                throw new ArgumentOutOfRangeException(nameof(lengthBytes), "HKDF length is too large.");
            }

            byte[] output = new byte[lengthBytes];
            HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, output, salt, info);
            return output;
        }

        /// <summary>
        /// String-based wrapper for compatibility: masterKey + purpose -> subkey.
        /// </summary>
        public static byte[] DeriveSubkey(
            string masterKey,
            string purpose,
            int lengthBytes,
            string? salt = null)
        {
            ArgumentNullException.ThrowIfNull(masterKey);
            ArgumentNullException.ThrowIfNull(purpose);
            if (lengthBytes == 0)
            {
                return [];
            }

            byte[] masterBytes = Encoding.UTF8.GetBytes(masterKey);
            byte[] infoBytes = Encoding.UTF8.GetBytes(purpose);
            byte[]? saltBytes = salt is null ? null : Encoding.UTF8.GetBytes(salt);

            try
            {
                return DeriveSubkey(
                    masterBytes,
                    infoBytes,
                    lengthBytes,
                    saltBytes is null ? [] : saltBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterBytes);
                CryptographicOperations.ZeroMemory(infoBytes);
                if (saltBytes is not null)
                {
                    CryptographicOperations.ZeroMemory(saltBytes);
                }
            }
        }

        public static string DeriveSubkeyBase64(
            string masterKey,
            string purpose,
            int lengthBytes,
            string? salt = null)
        {
            byte[] bytes = DeriveSubkey(masterKey, purpose, lengthBytes, salt);
            try
            {
                return Convert.ToBase64String(bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }
}
