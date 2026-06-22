// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;

namespace Cotton.Crypto.Helpers
{
    /// <summary>
    /// Provides helper methods for generating cryptographically secure random data.
    /// </summary>
    public static class RandomHelpers
    {
        /// <summary>
        /// Generates a cryptographically strong sequence of random bytes.
        /// </summary>
        /// <param name="length">Number of random bytes to generate.</param>
        /// <returns>A new array of <paramref name="length"/> random bytes.</returns>
        public static byte[] GetRandomBytes(int length)
        {
            var randomBytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return randomBytes;
        }
    }
}
