using System.Text;
using System.Security;
using System.Security.Cryptography;

namespace Cotton.Crypto
{
    public static class KeyDerivation
    {
        public static byte[] DeriveSubkey(string masterKey, string purpose, int lengthBytes)
        {
            // masterKey + purpose -> HMAC-SHA256 -> target length
            var masterBytes = Encoding.UTF8.GetBytes(masterKey);
            var purposeBytes = Encoding.UTF8.GetBytes(purpose);

            using var hmac = new HMACSHA256(masterBytes);
            var hash = hmac.ComputeHash(purposeBytes);

            if (lengthBytes <= hash.Length)
            {
                var result = new byte[lengthBytes];
                Array.Copy(hash, result, lengthBytes);
                return result;
            }

            // If you need more than 32 bytes, you can simply "pull" more blocks
            // with different counters. This allows for generating longer keys
            // while still being deterministic and tied to the master key and purpose.
            var buffer = new byte[lengthBytes];
            int offset = 0;
            int counter = 1;

            while (offset < lengthBytes)
            {
                var counterBytes = BitConverter.GetBytes(counter++);
                var blockInput = purposeBytes.Concat(counterBytes).ToArray();
                var block = hmac.ComputeHash(blockInput);

                int toCopy = Math.Min(block.Length, lengthBytes - offset);
                Array.Copy(block, 0, buffer, offset, toCopy);
                offset += toCopy;
            }

            return buffer;
        }

        public static string DeriveSubkeyBase64(string masterKey, string purpose, int lengthBytes)
        {
            var bytes = DeriveSubkey(masterKey, purpose, lengthBytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
