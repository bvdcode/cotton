using Cotton.Server.Services;
using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;

namespace Cotton.Server.Extensions
{
    public static class CryptoExtensions
    {
        public static byte[] DecryptPresignedToken(this IStreamCipher crypto, string token)
        {
            byte[] encrypted = Convert.FromBase64String(token);
            string decrypted = crypto.Decrypt(encrypted);
            string[] parts = decrypted.Split('|');
            if (parts.Length != 2)
            {
                throw new FormatException("Invalid token format.");
            }
            string hashStr = parts[0];
            string expireAtStr = parts[1];
            DateTime expireAt = DateTime.Parse(expireAtStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
            if (DateTime.UtcNow > expireAt)
            {
                throw new Exception("Token has expired.");
            }
            return Hasher.FromHexStringHash(hashStr);
        }

        public static string GetPresignedToken(this IStreamCipher crypto, byte[] hash, TimeSpan? expiration = null)
        {
            expiration ??= TimeSpan.FromDays(1);
            DateTime expireAt = DateTime.UtcNow.Add(expiration.Value);
            string hashStr = Hasher.ToHexStringHash(hash);
            string container = $"{hashStr}|{expireAt:R}";
            byte[] encrypted = crypto.Encrypt(container);
            return Convert.ToBase64String(encrypted);
        }
    }
}
