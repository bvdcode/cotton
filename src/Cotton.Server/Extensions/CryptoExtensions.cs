using Cotton.Server.Services;
using EasyExtensions.Extensions;
using EasyExtensions.Abstractions;

namespace Cotton.Server.Extensions
{
    public static class CryptoExtensions
    {
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
