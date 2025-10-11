using System.Buffers;
using System.Buffers.Binary;
using Cotton.Crypto.Internals;
using Cotton.Crypto.Abstractions;
using System.Security.Cryptography;
using Cotton.Crypto.Internals.Pipelines;

namespace Cotton.Crypto
{
    /// <summary>
    /// AES-GCM streaming cipher with per-file key wrapping and per-chunk authentication.
    /// Nonce layout: 12-byte IV = 4-byte file prefix || 8-byte chunk counter.
    /// The maximum number of chunks per file is 2^64-1; exceeding this throws InvalidOperationException to avoid IV reuse.
    /// </summary>
    public class AesGcmStreamCipher : IStreamCipher
    {
        private readonly int _keyId;
        private readonly byte[] _masterKeyBytes;
        public const int NonceSize = 12;
        public const int TagSize = 16;
        public const int KeySize = 32;
        public const int MinChunkSize = 8 * 1024;
        public const int MaxChunkSize = 64 * 1024 * 1024;
        public const int DefaultChunkSize = 16 * 1024 * 1024;
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;
        private readonly int ConcurrencyLevel = Math.Min(4, Environment.ProcessorCount);
        private readonly int _threadsMultiplier;
        private readonly int _maxThreads;
        private readonly int _windowCap;
        private readonly bool _strictLengthCheck;
        private readonly RandomNumberGenerator _rng;

        public AesGcmStreamCipher(ReadOnlyMemory<byte> masterKey, int keyId = 1, int? threads = null, int threadsLimitMultiplier = 2, int windowCap = 1024, bool strictLengthCheck = true, RandomNumberGenerator? rng = null)
        {
            if (masterKey.Length != KeySize)
            {
                throw new ArgumentException($"Master key must be {KeySize} bytes ({KeySize * 8} bits) long.", nameof(masterKey));
            }
            if (keyId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(keyId), "Key ID must be a positive integer.");
            }
            if (threadsLimitMultiplier < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(threadsLimitMultiplier), "Threads multiplier must be >= 1.");
            }
            if (windowCap < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(windowCap), "Window cap must be >= 4.");
            }

            _masterKeyBytes = masterKey.ToArray();
            _keyId = keyId;
            _threadsMultiplier = threadsLimitMultiplier;
            _maxThreads = Math.Max(1, Environment.ProcessorCount * _threadsMultiplier);
            _windowCap = windowCap;
            _strictLengthCheck = strictLengthCheck;
            _rng = rng ?? RandomNumberGenerator.Create();
            if (threads.HasValue)
            {
                if (threads.Value < 1 || threads.Value > _maxThreads)
                {
                    throw new ArgumentOutOfRangeException(nameof(threads), $"Threads must be between 1 and {_maxThreads} (CPU * {_threadsMultiplier}).");
                }
                ConcurrencyLevel = threads.Value;
            }
            ConcurrencyLevel = Math.Clamp(ConcurrencyLevel, 1, _maxThreads);
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

            byte[] fileKey = BufferPool.Rent(KeySize);
            try
            {
                _rng.GetBytes(fileKey.AsSpan(0, KeySize));
                // Per-file nonce prefix (4 bytes)
                Span<byte> prefixBytes = stackalloc byte[4];
                _rng.GetBytes(prefixBytes);
                uint fileNoncePrefix = BinaryPrimitives.ReadUInt32LittleEndian(prefixBytes);

                byte[] fileKeyNonce = new byte[NonceSize];
                Tag128 fileKeyTag;
                _rng.GetBytes(fileKeyNonce);
                byte[] encryptedFileKey = new byte[KeySize];
                using (var gcm = new AesGcm(_masterKeyBytes, TagSize))
                {
                    Span<byte> tagSpan = stackalloc byte[TagSize];
                    gcm.Encrypt(fileKeyNonce, fileKey.AsSpan(0, KeySize), encryptedFileKey, tagSpan);
                    fileKeyTag = Tag128.FromSpan(tagSpan);
                }

                long totalPlaintextLength = input.CanSeek ? Math.Max(0, input.Length - input.Position) : 0;
                int headerLen = AesGcmStreamFormat.ComputeFileHeaderLength(NonceSize, TagSize, KeySize);
                byte[] headerBuf = BufferPool.Rent(headerLen);
                try
                {
                    AesGcmStreamFormat.BuildFileHeader(headerBuf.AsSpan(0, headerLen), _keyId, fileNoncePrefix, fileKeyNonce, fileKeyTag, encryptedFileKey, totalPlaintextLength, NonceSize, TagSize, KeySize);
                    await output.WriteAsync(headerBuf.AsMemory(0, headerLen), ct).ConfigureAwait(false);
                }
                finally
                {
                    BufferPool.Return(headerBuf, clearArray: false);
                }

                var enc = new EncryptionPipeline(input, output, fileKey, fileNoncePrefix, chunkSize, ConcurrencyLevel, _keyId, NonceSize, TagSize, _windowCap, BufferPool);
                await enc.RunAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(fileKey);
                BufferPool.Return(fileKey, clearArray: false);
            }
        }

        public async Task DecryptAsync(Stream input, Stream output, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(output);
            if (!input.CanRead) throw new ArgumentException("Input stream must be readable.", nameof(input));
            if (!output.CanWrite) throw new ArgumentException("Output stream must be writable.", nameof(output));

            FileHeader header = await AesGcmStreamFormat.ReadFileHeaderAsync(input, NonceSize, TagSize, KeySize, ct).ConfigureAwait(false);
            if (header.KeyId != _keyId)
                throw new InvalidDataException($"Key ID mismatch. Expected {_keyId}, but file has {header.KeyId}.");

            byte[] fileKey = BufferPool.Rent(KeySize);
            try
            {
                using (var gcm = new AesGcm(_masterKeyBytes, TagSize))
                {
                    Span<byte> tagSpan = stackalloc byte[TagSize];
                    header.Tag.CopyTo(tagSpan);
                    gcm.Decrypt(header.Nonce, header.EncryptedKey, tagSpan, fileKey.AsSpan(0, KeySize));
                }
                var dec = new DecryptionPipeline(input, output, fileKey, header.NoncePrefix, ConcurrencyLevel, _keyId, NonceSize, TagSize, MaxChunkSize, _windowCap, header.TotalPlaintextLength, _strictLengthCheck, BufferPool);
                await dec.RunAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(fileKey);
                BufferPool.Return(fileKey, clearArray: false);
            }
        }
    }
}
