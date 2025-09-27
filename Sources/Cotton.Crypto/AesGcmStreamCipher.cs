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
            AesGcmKeyHeader keyHeader = AesGcmKeyHeader.FromStream(input, NonceSize, TagSize);

            using var gcm = new AesGcm(_masterKey.Span, TagSize);
            byte[] fileKey = new byte[32];
            gcm.Decrypt(keyHeader.Nonce, keyHeader.EncryptedKey, keyHeader.Tag, fileKey);

            using var fileGcm = new AesGcm(fileKey, TagSize);
            byte[] chunkNonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] buffer = new byte[DefaultChunkSize];
            int bytesRead;
            while ((bytesRead = input.Read(chunkNonce, 0, chunkNonce.Length)) > 0)
            {
                if (bytesRead < NonceSize)
                {
                    throw new InvalidDataException("Incomplete nonce read from stream.");
                }
                bytesRead = input.Read(tag, 0, tag.Length);
                if (bytesRead < TagSize)
                {
                    throw new InvalidDataException("Incomplete tag read from stream.");
                }
                bytesRead = input.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break; // End of stream
                }
                byte[] decryptedChunk = new byte[bytesRead];
                // public void Decrypt(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext, ReadOnlySpan<byte> associatedData = default);
                fileGcm.Decrypt(chunkNonce, buffer.AsSpan(0, bytesRead), tag, decryptedChunk);
                output.Write(decryptedChunk, 0, decryptedChunk.Length);
            }
            return Task.CompletedTask;
        }

        public async Task EncryptAsync(Stream input, Stream output, int chunkSize = DefaultChunkSize, CancellationToken ct = default)
        {
            if (chunkSize < MinChunkSize || chunkSize > MaxChunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), $"Chunk size must be between {MinChunkSize} and {MaxChunkSize} bytes.");
            }

            byte[] nonce = RandomHelpers.GetRandomBytes(12);
            using var gcm = new AesGcm(_masterKey.Span, TagSize);

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
                AesGcmKeyHeader chunkHeader = new(_keyId, chunkNonce, tag, [], bytesRead);
                output.Write(chunkHeader.ToBytes().Span);
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
