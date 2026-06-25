// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;

namespace Cotton.Crypto
{
    /// <summary>
    /// Provides hash helpers used by Cotton cryptographic call sites.
    /// </summary>
    public static class HashExtensions
    {
        /// <summary>
        /// Computes a lowercase hexadecimal SHA-256 hash for the specified UTF-8 text.
        /// </summary>
        public static string Sha256(this string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
