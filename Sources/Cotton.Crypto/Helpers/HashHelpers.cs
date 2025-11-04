using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Cotton.Crypto.Helpers
{
    public static partial class HashHelpers
    {
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
