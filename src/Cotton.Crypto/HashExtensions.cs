// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;

namespace Cotton.Crypto
{
    public static class HashExtensions
    {
        public static string Sha256(this string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
