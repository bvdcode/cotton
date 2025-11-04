using System.Security.Cryptography;

namespace Cotton.Crypto
{
    public class Hasher
    {
        public static string SupportedHashAlgorithm => nameof(SHA256);
    }
}
