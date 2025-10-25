// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Cotton.Server.Helpers
{
    public static partial class HashHelpers
    {
        public static string SupportedHashAlgorithm => nameof(SHA256);

        public static bool IsValidHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }
            return Sha256Regex().IsMatch(hash);
        }

        public static string HashToHex(Stream input)
        {
            byte[] result = SHA256.HashData(input);
            return Convert.ToHexString(result).ToLowerInvariant();
        }

        [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.Compiled)]
        private static partial Regex Sha256Regex();
    }
}
