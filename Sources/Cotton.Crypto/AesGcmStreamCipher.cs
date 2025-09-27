using Cotton.Crypto.Models;
using Cotton.Crypto.Helpers;
using Cotton.Crypto.Abstractions;
using System.Security.Cryptography;

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

        public async Task EncryptAsync(Stream input, Stream output, int chunkSize = DefaultChunkSize, CancellationToken ct = default)
        {
            if (chunkSize < MinChunkSize || chunkSize > MaxChunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), $"Chunk size must be between {MinChunkSize} and {MaxChunkSize} bytes.");
            }

            byte[] nonce = RandomHelpers.GetRandomBytes(12);
            using var gcm = new AesGcm(_masterKey.Span, TagSize);
            gcm.Encrypt(nonce, _masterKey.Span, nonce, nonce);

            AesGcmKeyHeader keyHeader = EncryptFileKey(gcm, nonce);
            output.Write(keyHeader.ToBytes().Span);

            byte[] buffer = new byte[chunkSize];
            int bytesRead;
            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                byte[] chunkNonce = RandomHelpers.GetRandomBytes(NonceSize);
                byte[] tag = new byte[TagSize];
                byte[] encryptedChunk = new byte[bytesRead];
                gcm.Encrypt(chunkNonce, buffer.AsSpan(0, bytesRead), encryptedChunk, tag);
                await output.WriteAsync(chunkNonce, ct);
                await output.WriteAsync(tag, ct);
                await output.WriteAsync(encryptedChunk, ct);
            }
        }

        private AesGcmKeyHeader EncryptFileKey(AesGcm gcm, byte[] nonce)
        {
            byte[] fileKey = RandomHelpers.GetRandomBytes(32);
            byte[] encryptedFileKey = new byte[fileKey.Length];
            byte[] tag = new byte[TagSize];
            gcm.Encrypt(nonce, fileKey, encryptedFileKey, tag);
            return new AesGcmKeyHeader(KeyId: _keyId, Nonce: nonce, Tag: tag, EncryptedKey: encryptedFileKey);
        }
    }
}
