using System.Buffers;
using Cotton.Crypto.Models;
using Cotton.Crypto.Helpers;
using Cotton.Crypto.Abstractions;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Threading.Channels;

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
        private const int DefaultChunkSize = 4_194_304; // 4 MB - balance between memory and efficiency  
        private const int MinChunkSize = 65_536; // 64 KB
        private const int MaxChunkSize = 16_777_216; // 16 MB

        // Performance optimization: Use shared buffer pools
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;
        private static readonly int ConcurrencyLevel = Math.Max(2, Environment.ProcessorCount / 2);

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
            
            if (!input.CanRead) 
            {
                throw new ArgumentException("Input stream must be readable.", nameof(input));
            }
            
            if (!output.CanWrite) 
            {
                throw new ArgumentException("Output stream must be writable.", nameof(output));
            }

            AesGcmKeyHeader keyHeader = AesGcmKeyHeader.FromStream(input, NonceSize, TagSize);
            byte[] fileKey = BufferPool.Rent(KeySize);

            try
            {
                using (AesGcm gcm = new AesGcm(_masterKey.Span, TagSize))
                {
                    gcm.Decrypt(keyHeader.Nonce, keyHeader.EncryptedKey, keyHeader.Tag, fileKey.AsSpan(0, KeySize));
                }

                using AesGcm fileGcm = new AesGcm(fileKey.AsSpan(0, KeySize), TagSize);
                
                if (!input.CanSeek || (keyHeader.DataLength > 0 && keyHeader.DataLength <= DefaultChunkSize))
                {
                    await DecryptSequentialAsync(input, output, fileGcm, ct).ConfigureAwait(false);
                }
                else
                {
                    await DecryptSequentialAsync(input, output, fileGcm, ct).ConfigureAwait(false);
                }
            }
            finally
            {
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
            }
        }

        public async Task EncryptAsync(Stream input, Stream output, int chunkSize = DefaultChunkSize, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(output);
            
            if (!input.CanRead) 
            {
                throw new ArgumentException("Input stream must be readable.", nameof(input));
            }
            
            if (!output.CanWrite) 
            {
                throw new ArgumentException("Output stream must be writable.", nameof(output));
            }

            if (chunkSize < MinChunkSize || chunkSize > MaxChunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), 
                    $"Chunk size must be between {MinChunkSize} and {MaxChunkSize} bytes.");
            }

            byte[] fileKey = BufferPool.Rent(KeySize);

            try
            {
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(fileKey.AsSpan(0, KeySize));
                }

                byte[] fileKeyNonce = RandomHelpers.GetRandomBytes(NonceSize);
                byte[] encryptedFileKey = new byte[KeySize];
                byte[] fileKeyTag = new byte[TagSize];

                using (AesGcm gcm = new AesGcm(_masterKey.Span, TagSize))
                {
                    gcm.Encrypt(fileKeyNonce, fileKey.AsSpan(0, KeySize), encryptedFileKey, fileKeyTag);
                }

                long remainingLength = 0;
                if (input.CanSeek)
                {
                    remainingLength = Math.Max(0, input.Length - input.Position);
                }

                AesGcmKeyHeader keyHeader = new AesGcmKeyHeader(_keyId, fileKeyNonce, fileKeyTag, encryptedFileKey, remainingLength);
                ReadOnlyMemory<byte> headerBytes = keyHeader.ToBytes();
                await output.WriteAsync(headerBytes, ct).ConfigureAwait(false);

                byte[] fileKeyArray = new byte[KeySize];
                Array.Copy(fileKey, fileKeyArray, KeySize);
                
                using AesGcm sequentialGcm = new AesGcm(fileKey.AsSpan(0, KeySize), TagSize);
                await EncryptSequentialAsync(input, output, sequentialGcm, chunkSize, ct).ConfigureAwait(false);
            }
            finally
            {
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
            }
        }

        private async Task DecryptSequentialAsync(Stream input, Stream output, AesGcm fileGcm, CancellationToken ct)
        {
            byte[] cipherBuffer = BufferPool.Rent(MaxChunkSize);
            byte[] plainBuffer = BufferPool.Rent(MaxChunkSize);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (input.CanSeek && input.Position >= input.Length)
                    {
                        break;
                    }

                    AesGcmKeyHeader chunkHeader;
                    try
                    {
                        chunkHeader = AesGcmKeyHeader.FromStream(input, NonceSize, TagSize);
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }

                    if (chunkHeader.DataLength < 0 || chunkHeader.DataLength > MaxChunkSize)
                    {
                        throw new InvalidDataException("Invalid chunk length in header.");
                    }

                    int cipherLen = (int)chunkHeader.DataLength;
                    
                    ReadExactly(input, cipherBuffer, cipherLen);
                    fileGcm.Decrypt(chunkHeader.Nonce, cipherBuffer.AsSpan(0, cipherLen), chunkHeader.Tag, plainBuffer.AsSpan(0, cipherLen));
                    await output.WriteAsync(plainBuffer.AsMemory(0, cipherLen), ct).ConfigureAwait(false);
                }
            }
            finally
            {
                BufferPool.Return(cipherBuffer);
                BufferPool.Return(plainBuffer);
            }
        }

        private async Task EncryptSequentialAsync(Stream input, Stream output, AesGcm fileGcm, int chunkSize, CancellationToken ct)
        {
            byte[] readBuffer = BufferPool.Rent(chunkSize);
            byte[] encryptedBuffer = BufferPool.Rent(chunkSize);

            try
            {
                using var rng = RandomNumberGenerator.Create();
                
                int bytesRead;
                while ((bytesRead = input.Read(readBuffer, 0, chunkSize)) > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    var nonce = new byte[NonceSize];
                    var tag = new byte[TagSize];
                    rng.GetBytes(nonce);

                    fileGcm.Encrypt(nonce, readBuffer.AsSpan(0, bytesRead), encryptedBuffer.AsSpan(0, bytesRead), tag);

                    var chunkHeader = new AesGcmKeyHeader(_keyId, nonce, tag, [], bytesRead);
                    var headerBytes = chunkHeader.ToBytes();

                    await output.WriteAsync(headerBytes, ct).ConfigureAwait(false);
                    await output.WriteAsync(encryptedBuffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                }
            }
            finally
            {
                BufferPool.Return(readBuffer);
                BufferPool.Return(encryptedBuffer);
            }
        }

        private async Task EncryptParallelAsync(Stream input, Stream output, byte[] fileKey, int chunkSize, long totalSize, CancellationToken ct)
        {
            // Create channels for pipeline processing
            var channel = Channel.CreateBounded<EncryptionJob>(ConcurrencyLevel * 2);
            var writer = channel.Writer;
            var reader = channel.Reader;

            // Create ordered results channel
            var resultsChannel = Channel.CreateBounded<EncryptionResult>(ConcurrencyLevel * 2);
            var resultsWriter = resultsChannel.Writer;
            var resultsReader = resultsChannel.Reader;

            // Producer task - reads chunks from input stream
            var producerTask = Task.Run(async () =>
            {
                try
                {
                    int chunkIndex = 0;
                    byte[] readBuffer = BufferPool.Rent(chunkSize);
                    
                    try
                    {
                        int bytesRead;
                        while ((bytesRead = input.Read(readBuffer, 0, chunkSize)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();

                            var chunkData = new byte[bytesRead];
                            Array.Copy(readBuffer, chunkData, bytesRead);

                            var job = new EncryptionJob(chunkIndex++, chunkData, bytesRead);
                            await writer.WriteAsync(job, ct).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        BufferPool.Return(readBuffer);
                    }
                }
                finally
                {
                    writer.Complete();
                }
            }, ct);

            // Worker tasks - encrypt chunks in parallel
            var workerTasks = new List<Task>();
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workerTasks.Add(Task.Run(async () =>
                {
                    using var workerGcm = new AesGcm(fileKey, TagSize);
                    using var rng = RandomNumberGenerator.Create();

                    await foreach (var job in reader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();

                        byte[] encryptedBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            var nonce = new byte[NonceSize];
                            var tag = new byte[TagSize];
                            rng.GetBytes(nonce);

                            workerGcm.Encrypt(nonce, job.Data.AsSpan(0, job.DataLength), encryptedBuffer.AsSpan(0, job.DataLength), tag);

                            var chunkHeader = new AesGcmKeyHeader(_keyId, nonce, tag, [], job.DataLength);
                            var headerBytes = chunkHeader.ToBytes();

                            var result = new EncryptionResult(job.Index, headerBytes.ToArray(), encryptedBuffer, job.DataLength);
                            await resultsWriter.WriteAsync(result, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            BufferPool.Return(encryptedBuffer);
                            throw;
                        }
                    }
                }, ct));
            }

            // Consumer task - writes results in order
            var consumerTask = Task.Run(async () =>
            {
                var pendingResults = new Dictionary<int, EncryptionResult>();
                int expectedIndex = 0;

                await foreach (var result in resultsReader.ReadAllAsync(ct))
                {
                    pendingResults[result.Index] = result;

                    while (pendingResults.TryGetValue(expectedIndex, out var nextResult))
                    {
                        try
                        {
                            await output.WriteAsync(nextResult.Header, ct).ConfigureAwait(false);
                            await output.WriteAsync(nextResult.Data.AsMemory(0, nextResult.DataLength), ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            BufferPool.Return(nextResult.Data);
                        }

                        pendingResults.Remove(expectedIndex);
                        expectedIndex++;
                    }
                }
            }, ct);

            try
            {
                // Wait for producer to complete, then signal workers
                await producerTask.ConfigureAwait(false);
                
                // Wait for all workers to complete
                await Task.WhenAll(workerTasks).ConfigureAwait(false);
            }
            finally
            {
                // Always complete the results writer
                resultsWriter.TryComplete();
            }

            // Wait for consumer to complete
            await consumerTask.ConfigureAwait(false);
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

        private readonly record struct EncryptionJob(int Index, byte[] Data, int DataLength);
        private readonly record struct EncryptionResult(int Index, byte[] Header, byte[] Data, int DataLength);
    }
}
