using System.Buffers;
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

        // Cryptographic constants
        private const int NonceSize = 12; // 96 bits
        private const int TagSize = 16; // 128 bits
        private const int KeySize = 32; // 256 bits AES key

        // Optimized chunk sizes - use maximum allowed for better performance
        private const int DefaultChunkSize = MaxChunkSize; // 16 MB (maximum)
        private const int MinChunkSize = 65_536; // 64 KB
        private const int MaxChunkSize = 16_777_216; // 16 MB

        // Performance optimization: Use shared buffer pools
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

        public AesGcmStreamCipher(ReadOnlyMemory<byte> masterKey, int keyId = 1)
        {
            if (masterKey.Length != KeySize)
            {
                throw new ArgumentException($"Master key must be {KeySize} bytes ({KeySize * 8} bits) long.", nameof(masterKey));
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
            var keyHeader = AesGcmKeyHeader.FromStream(input, NonceSize, TagSize);
            var fileKey = BufferPool.Rent(KeySize);

            try
            {
                using (var gcm = new AesGcm(_masterKey.Span, TagSize))
                {
                    gcm.Decrypt(keyHeader.Nonce, keyHeader.EncryptedKey, keyHeader.Tag, fileKey.AsSpan(0, KeySize));
                }

                using var fileGcm = new AesGcm(fileKey.AsSpan(0, KeySize), TagSize);
                await DecryptChunksSyncAsync(input, output, fileGcm, ct).ConfigureAwait(false);
            }
            finally
            {
                // Clear sensitive data before returning to pool
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
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
            var fileKey = BufferPool.Rent(KeySize);

            try
            {
                // Generate file key directly into rented buffer
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(fileKey.AsSpan(0, KeySize));

                var fileKeyNonce = RandomHelpers.GetRandomBytes(NonceSize);
                var encryptedFileKey = new byte[KeySize];
                var fileKeyTag = new byte[TagSize];

                using (var gcm = new AesGcm(_masterKey.Span, TagSize))
                {
                    gcm.Encrypt(fileKeyNonce, fileKey.AsSpan(0, KeySize), encryptedFileKey, fileKeyTag);
                }

                long remainingLength = 0;
                if (input.CanSeek)
                {
                    remainingLength = Math.Max(0, input.Length - input.Position);
                }

                // Write master/file key header
                var keyHeader = new AesGcmKeyHeader(_keyId, fileKeyNonce, fileKeyTag, encryptedFileKey, remainingLength);
                var headerBytes = keyHeader.ToBytes();
                await output.WriteAsync(headerBytes, ct).ConfigureAwait(false);

                // Encrypt and write chunks with optimizations
                using var fileGcm = new AesGcm(fileKey.AsSpan(0, KeySize), TagSize);
                await EncryptChunksSyncAsync(input, output, fileGcm, chunkSize, ct).ConfigureAwait(false);
            }
            finally
            {
                // Clear sensitive data before returning to pool
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
            }
        }

        private async Task DecryptChunksSyncAsync(Stream input, Stream output, AesGcm fileGcm, CancellationToken ct)
        {
            var cipherBuffer = BufferPool.Rent(MaxChunkSize);
            var plainBuffer = BufferPool.Rent(MaxChunkSize);

            try
            {
                while (!ct.IsCancellationRequested)
                {
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
                    
                    // Read ciphertext synchronously for better performance
                    ReadExactly(input, cipherBuffer, cipherLen);
                    
                    // Decrypt in-place using spans
                    fileGcm.Decrypt(chunkHeader.Nonce, cipherBuffer.AsSpan(0, cipherLen), chunkHeader.Tag, plainBuffer.AsSpan(0, cipherLen));
                    
                    // Write plaintext synchronously
                    await output.WriteAsync(plainBuffer.AsMemory(0, cipherLen), ct).ConfigureAwait(false);
                }
            }
            finally
            {
                BufferPool.Return(cipherBuffer);
                BufferPool.Return(plainBuffer);
            }
        }

        private async Task EncryptChunksSyncAsync(Stream input, Stream output, AesGcm fileGcm, int chunkSize, CancellationToken ct)
        {
            var readBuffer = BufferPool.Rent(chunkSize);
            var encryptedBuffer = BufferPool.Rent(chunkSize);
            var nonceBuffer = BufferPool.Rent(NonceSize);
            var tagBuffer = new byte[TagSize]; // Keep on stack since it's small

            try
            {
                using var rng = RandomNumberGenerator.Create();
                
                int bytesRead;
                // Use sync read for better performance on MemoryStream
                while ((bytesRead = input.Read(readBuffer, 0, chunkSize)) > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    // Generate nonce directly into buffer
                    rng.GetBytes(nonceBuffer.AsSpan(0, NonceSize));

                    // Encrypt using spans for better performance
                    fileGcm.Encrypt(nonceBuffer.AsSpan(0, NonceSize), readBuffer.AsSpan(0, bytesRead), encryptedBuffer.AsSpan(0, bytesRead), tagBuffer);

                    // Create chunk header and write efficiently
                    var chunkHeader = new AesGcmKeyHeader(_keyId, nonceBuffer.AsSpan(0, NonceSize).ToArray(), tagBuffer, [], bytesRead);
                    var headerBytes = chunkHeader.ToBytes();

                    // Write header and data
                    await output.WriteAsync(headerBytes, ct).ConfigureAwait(false);
                    await output.WriteAsync(encryptedBuffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                }
            }
            finally
            {
                BufferPool.Return(readBuffer);
                BufferPool.Return(encryptedBuffer);
                BufferPool.Return(nonceBuffer);
            }
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int count)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = stream.Read(buffer, totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream.");
                }
                totalBytesRead += bytesRead;
            }
        }

        private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalBytesRead, count - totalBytesRead), ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream.");
                }
                totalBytesRead += bytesRead;
            }
        }
    }
}
