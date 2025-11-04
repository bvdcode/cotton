using System.Security.Cryptography;

namespace Cotton.Crypto
{
    public class Hasher
    {
        public static string SupportedHashAlgorithm => nameof(SHA256);

        public const int HashSizeInBytes = 32;

        public static byte[] HashData(byte[] content)
        {
            return SHA256.HashData(content);
        }

        public static byte[] HashData(Stream input)
        {
            return SHA256.HashData(input);
        }

        public static async Task<byte[]> HashDataAsync(Stream stream)
        {
            return await SHA256.HashDataAsync(stream);
        }
    }
}
