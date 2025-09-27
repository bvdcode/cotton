using System.Security.Cryptography;

namespace Cotton.Crypto.Helpers
{
    public static class RandomHelpers
    {
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
