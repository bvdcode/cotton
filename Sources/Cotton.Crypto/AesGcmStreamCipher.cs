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

        public async Task DecryptAsync(Stream input, Stream output, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(output);
            if (!input.CanRead) throw new ArgumentException("Input stream must be readable.", nameof(input));
            if (!output.CanWrite) throw new ArgumentException("Output stream must be writable.", nameof(output));

            // Read master/file key header and unwrap file key
            AesGcmKeyHeader keyHeader = AesGcmKeyHeader.FromStream(input, NonceSize, TagSize);
            byte[] fileKey = new byte[32];
            using (var gcm = new AesGcm(_masterKey.Span, TagSize))
            {
                gcm.Decrypt(keyHeader.Nonce, keyHeader.EncryptedKey, keyHeader.Tag, fileKey);
            }

            using var fileGcm = new AesGcm(fileKey, TagSize);

            // Read chunks until EOF
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                if (input.CanSeek && input.Position >= input.Length)
                {
                    break; // normal end of stream
                }

                // Each chunk is preceded by a header with Nonce/Tag and dataLength = ciphertext length
                AesGcmKeyHeader chunkHeader;
                try
                {
                    chunkHeader = AesGcmKeyHeader.FromStream(input, NonceSize, TagSize);
                }
                catch (EndOfStreamException)
                {
                    break; // reached end cleanly
                }

                if (chunkHeader.DataLength < 0 || chunkHeader.DataLength > MaxChunkSize)
                {
                    throw new InvalidDataException("Invalid chunk length in header.");
                }

                int cipherLen = (int)chunkHeader.DataLength;
                byte[] ciphertext = new byte[cipherLen];
                ReadExactly(input, ciphertext, 0, cipherLen);

                byte[] plaintext = new byte[cipherLen];
                fileGcm.Decrypt(chunkHeader.Nonce, ciphertext, chunkHeader.Tag, plaintext);
                await output.WriteAsync(plaintext, ct).ConfigureAwait(false);
            }
        }

        public async Task EncryptAsync(Stream input, Stream output, int chunkSize = DefaultChunkSize, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(output);
            if (!input.CanRead) throw new ArgumentException("Input stream must be readable.", nameof(input));
            if (!output.CanWrite) throw new ArgumentException("Output stream must be writable.", nameof(output));

            if (chunkSize < MinChunkSize || chunkSize > MaxChunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), $"Chunk size must be between {MinChunkSize} and {MaxChunkSize} bytes.");
            }

            // Generate per-file key and wrap it using master key
            byte[] fileKey = RandomHelpers.GetRandomBytes(32);
            byte[] fileKeyNonce = RandomHelpers.GetRandomBytes(NonceSize);
            byte[] encryptedFileKey = new byte[fileKey.Length];
            byte[] fileKeyTag = new byte[TagSize];
            using (var gcm = new AesGcm(_masterKey.Span, TagSize))
            {
                gcm.Encrypt(fileKeyNonce, fileKey, encryptedFileKey, fileKeyTag);
            }

            long remainingLength = 0;
            if (input.CanSeek)
            {
                remainingLength = Math.Max(0, input.Length - input.Position);
            }

            // Write master/file key header
            AesGcmKeyHeader keyHeader = new(_keyId, fileKeyNonce, fileKeyTag, encryptedFileKey, remainingLength);
            await output.WriteAsync(keyHeader.ToBytes(), ct).ConfigureAwait(false);

            // Encrypt and write chunks
            using var fileGcm = new AesGcm(fileKey, TagSize);
            byte[] buffer = new byte[chunkSize];
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                ct.ThrowIfCancellationRequested();

                byte[] chunkNonce = RandomHelpers.GetRandomBytes(NonceSize);
                byte[] tag = new byte[TagSize];
                byte[] encryptedChunk = new byte[bytesRead];
                fileGcm.Encrypt(chunkNonce, buffer.AsSpan(0, bytesRead), encryptedChunk, tag);

                // Chunk header: contains nonce/tag and declares the following ciphertext length
                AesGcmKeyHeader chunkHeader = new(_keyId, chunkNonce, tag, [], bytesRead);
                await output.WriteAsync(chunkHeader.ToBytes(), ct).ConfigureAwait(false);
                await output.WriteAsync(encryptedChunk, ct).ConfigureAwait(false);
            }
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, offset + total, count - total);
                if (read == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream.");
                }
                total += read;
            }
        }
    }
}
