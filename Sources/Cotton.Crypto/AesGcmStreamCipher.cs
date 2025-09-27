using Cotton.Crypto.Helpers;
using Cotton.Crypto.Abstractions;
using System.Security.Cryptography;
using Cotton.Crypto.Models;

namespace Cotton.Crypto
{
    public class AesGcmStreamCipher : IStreamCipher
    {
        private readonly int _keyId;
        private readonly ReadOnlyMemory<byte> _masterKey;

        private const int NonceSize = 12; // 96 bits
        private const int TagSize = 16; // 128 bits

        private const int DefaultChunkSize = 1_048_576; // 1 MB
        private const int MinChunkSize = 65_536; // 64 KB
        private const int MaxChunkSize = 16_777_216; // 16 MB


        public AesGcmStreamCipher(ReadOnlyMemory<byte> masterKey, int keyId = 1)
        {
            if (masterKey.Length != 32)
            {
                throw new ArgumentException("Master key must be 32 bytes (256 bits) long.", nameof(masterKey));
            }
            _masterKey = masterKey;
            if (keyId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(keyId), "Key ID must be a positive integer.");
            }
            _keyId = keyId;
        }

        public Task DecryptAsync(Stream input, Stream output, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task EncryptAsync(Stream input, Stream output, int chunkSize = DefaultChunkSize, CancellationToken ct = default)
        {
            byte[] nonce = RandomHelpers.GetRandomBytes(12);
            using var gcm = new AesGcm(_masterKey.Span, TagSize);
            gcm.Encrypt(nonce, _masterKey.Span, nonce, nonce);



            byte[] encryptedFileKey = EncryptFileKey(gcm, nonce);
            CryptoHeader header = new(KeyId: _keyId, Nonce: nonce, EncryptedFileKey: encryptedFileKey);



        }

        private static byte[] EncryptFileKey(AesGcm gcm, byte[] nonce)
        {
            byte[] fileKey = RandomHelpers.GetRandomBytes(32);
            byte[] encryptedFileKey = new byte[fileKey.Length];
            byte[] tag = new byte[TagSize];
            gcm.Encrypt(nonce, fileKey, encryptedFileKey, tag);
            return encryptedFileKey;
        }
    }
}
