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
        private const int DefaultChunkSize = 8_388_608; // 8 MB
        private const int MinChunkSize = 65_536; // 64 KB
        private const int MaxChunkSize = 16_777_216; // 16 MB

        // Performance optimization: Use shared buffer pools
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;
        private static readonly int ConcurrencyLevel = Math.Max(2, Environment.ProcessorCount);

        // Magic header bytes (ASCII for "CTN1")
        private static ReadOnlySpan<byte> MagicBytes => "CTN1"u8;

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
                using (AesGcm gcm = new(_masterKey.Span, TagSize))
                {
                    gcm.Decrypt(keyHeader.Nonce, keyHeader.EncryptedKey, keyHeader.Tag, fileKey.AsSpan(0, KeySize));
                }

                // If we know the total data length and it's large, process in parallel
                bool canParallel = input.CanSeek && keyHeader.DataLength >= (DefaultChunkSize * 4);

                if (canParallel)
                {
                    await DecryptParallelAsync(input, output, fileKey.AsSpan(0, KeySize).ToArray(), ct).ConfigureAwait(false);
                }
                else
                {
                    using AesGcm fileGcm = new(fileKey.AsSpan(0, KeySize), TagSize);
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
                RandomNumberGenerator.Fill(fileKey.AsSpan(0, KeySize));

                Span<byte> fileKeyNonce = stackalloc byte[NonceSize];
                Span<byte> fileKeyTag = stackalloc byte[TagSize];
                RandomNumberGenerator.Fill(fileKeyNonce);

                byte[] encryptedFileKey = new byte[KeySize];

                using (AesGcm gcm = new(_masterKey.Span, TagSize))
                {
                    gcm.Encrypt(fileKeyNonce, fileKey.AsSpan(0, KeySize), encryptedFileKey, fileKeyTag);
                }

                long remainingLength = 0;
                if (input.CanSeek)
                {
                    remainingLength = Math.Max(0, input.Length - input.Position);
                }

                // Write file header directly (avoid allocations)
                WriteFileHeader(output, _keyId, fileKeyNonce, fileKeyTag, encryptedFileKey, remainingLength);

                // Choose encryption strategy
                bool canParallel = input.CanSeek && remainingLength >= (long)chunkSize * 4;
                if (canParallel)
                {
                    // Copy key into an array which will be used by workers to create their own AesGcm instances
                    byte[] fileKeyArray = new byte[KeySize];
                    Array.Copy(fileKey, fileKeyArray, KeySize);
                    await EncryptParallelAsync(input, output, fileKeyArray, chunkSize, remainingLength, ct).ConfigureAwait(false);
                }
                else
                {
                    using AesGcm sequentialGcm = new(fileKey.AsSpan(0, KeySize), TagSize);
                    await EncryptSequentialAsync(input, output, sequentialGcm, chunkSize, ct).ConfigureAwait(false);
                }
            }
            finally
            {
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
            }
        }

        private static async Task DecryptSequentialAsync(Stream input, Stream output, AesGcm fileGcm, CancellationToken ct)
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
                int bytesRead;
                while ((bytesRead = input.Read(readBuffer, 0, chunkSize)) > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    Span<byte> nonce = stackalloc byte[NonceSize];
                    Span<byte> tag = stackalloc byte[TagSize];
                    RandomNumberGenerator.Fill(nonce);

                    fileGcm.Encrypt(nonce, readBuffer.AsSpan(0, bytesRead), encryptedBuffer.AsSpan(0, bytesRead), tag);

                    // Write compact chunk header directly
                    WriteChunkHeader(output, _keyId, nonce, tag, bytesRead);

                    // Write ciphertext
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

                            // Rent a dedicated buffer for this job
                            byte[] chunkData = BufferPool.Rent(bytesRead);
                            Buffer.BlockCopy(readBuffer, 0, chunkData, 0, bytesRead);

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

                    await foreach (var job in reader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();

                        byte[] encryptedBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            Span<byte> nonce = stackalloc byte[NonceSize];
                            Span<byte> tag = stackalloc byte[TagSize];
                            RandomNumberGenerator.Fill(nonce);

                            workerGcm.Encrypt(nonce, job.Data.AsSpan(0, job.DataLength), encryptedBuffer.AsSpan(0, job.DataLength), tag);

                            // Copy nonce and tag to arrays for cross-task transport
                            byte[] nonceArr = new byte[NonceSize];
                            byte[] tagArr = new byte[TagSize];
                            nonce.CopyTo(nonceArr);
                            tag.CopyTo(tagArr);

                            var result = new EncryptionResult(job.Index, nonceArr, tagArr, encryptedBuffer, job.DataLength);
                            await resultsWriter.WriteAsync(result, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            BufferPool.Return(encryptedBuffer);
                            throw;
                        }
                        finally
                        {
                            // Return plaintext buffer asap
                            BufferPool.Return(job.Data);
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
                            // Write header directly
                            WriteChunkHeader(output, _keyId, nextResult.Nonce, nextResult.Tag, nextResult.DataLength);
                            // Write ciphertext
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
                // Wait for producer to complete
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

        private async Task DecryptParallelAsync(Stream input, Stream output, byte[] fileKey, CancellationToken ct)
        {
            // Read chunked input and dispatch to workers
            var channel = Channel.CreateBounded<DecryptionJob>(ConcurrencyLevel * 2);
            var writer = channel.Writer;
            var reader = channel.Reader;

            var resultsChannel = Channel.CreateBounded<DecryptionResult>(ConcurrencyLevel * 2);
            var resultsWriter = resultsChannel.Writer;
            var resultsReader = resultsChannel.Reader;

            // Producer reads headers + ciphertext
            var producerTask = Task.Run(async () =>
            {
                try
                {
                    int chunkIndex = 0;
                    while (!ct.IsCancellationRequested)
                    {
                        // Check if enough bytes remain for a header
                        if (input.CanSeek && input.Position >= input.Length)
                            break;
                        // Minimum header size: 4+4+8+4+NonceSize+TagSize
                        long bytesLeft = input.CanSeek ? (input.Length - input.Position) : 1;
                        if (input.CanSeek && bytesLeft < (4 + 4 + 8 + 4 + NonceSize + TagSize))
                            break;

                        AesGcmKeyHeader chunkHeader;
                        try
                        {
                            chunkHeader = AesGcmKeyHeader.FromStream(input, NonceSize, TagSize);
                        }
                        catch (EndOfStreamException)
                        {
                            break;
                        }

                        if (chunkHeader.DataLength <= 0 || chunkHeader.DataLength > MaxChunkSize)
                        {
                            throw new InvalidDataException("Invalid chunk length in header.");
                        }

                        int cipherLen = (int)chunkHeader.DataLength;
                        byte[] cipherBuffer = BufferPool.Rent(cipherLen);
                        ReadExactly(input, cipherBuffer, cipherLen);

                        var job = new DecryptionJob(chunkIndex++, chunkHeader.Nonce, chunkHeader.Tag, cipherBuffer, cipherLen);
                        await writer.WriteAsync(job, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    writer.Complete();
                }
            }, ct);

            // Workers decrypt
            var workerTasks = new List<Task>();
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workerTasks.Add(Task.Run(async () =>
                {
                    using var workerGcm = new AesGcm(fileKey, TagSize);

                    await foreach (var job in reader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();

                        byte[] plainBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            workerGcm.Decrypt(job.Nonce, job.Cipher.AsSpan(0, job.DataLength), job.Tag, plainBuffer.AsSpan(0, job.DataLength));
                            var result = new DecryptionResult(job.Index, plainBuffer, job.DataLength);
                            await resultsWriter.WriteAsync(result, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            BufferPool.Return(plainBuffer);
                            throw;
                        }
                        finally
                        {
                            BufferPool.Return(job.Cipher);
                        }
                    }
                }, ct));
            }

            // Consumer writes in order
            var consumerTask = Task.Run(async () =>
            {
                var pendingResults = new Dictionary<int, DecryptionResult>();
                int expectedIndex = 0;

                await foreach (var result in resultsReader.ReadAllAsync(ct))
                {
                    pendingResults[result.Index] = result;

                    while (pendingResults.TryGetValue(expectedIndex, out var nextResult))
                    {
                        try
                        {
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
                await producerTask.ConfigureAwait(false);
                await Task.WhenAll(workerTasks).ConfigureAwait(false);
            }
            finally
            {
                resultsWriter.TryComplete();
            }

            await consumerTask.ConfigureAwait(false);
        }

        private static void WriteFileHeader(Stream output, int keyId, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> encryptedKey, long dataLength)
        {
            // Magic (4) + HeaderLen (4) + DataLen (8) + KeyId (4) + Nonce (12) + Tag (16) + EncryptedKey (32)
            const int headerLength = 4 + 4 + 8 + 4 + NonceSize + TagSize + KeySize;
            Span<byte> header = stackalloc byte[headerLength];
            int offset = 0;

            // Magic
            MagicBytes.CopyTo(header.Slice(offset));
            offset += MagicBytes.Length;
            
            // Header Length (includes magic length like AesGcmKeyHeader does)
            BitConverter.TryWriteBytes(header.Slice(offset), headerLength);
            offset += sizeof(int);

            // Data Length
            BitConverter.TryWriteBytes(header.Slice(offset), dataLength);
            offset += sizeof(long);

            // KeyId
            BitConverter.TryWriteBytes(header.Slice(offset), keyId);
            offset += sizeof(int);

            // Nonce
            nonce.CopyTo(header.Slice(offset));
            offset += NonceSize;

            // Tag
            tag.CopyTo(header.Slice(offset));
            offset += TagSize;

            // Encrypted Key
            encryptedKey.CopyTo(header.Slice(offset));
            offset += encryptedKey.Length;

            output.Write(header);
        }

        private static void WriteChunkHeader(Stream output, int keyId, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, int dataLength)
        {
            // Magic (4) + HeaderLen (4) + DataLen (8) + KeyId (4) + Nonce (12) + Tag (16) + EncryptedKey (0)
            const int headerLength = 4 + 4 + 8 + 4 + NonceSize + TagSize + 0;
            Span<byte> header = stackalloc byte[headerLength];
            int offset = 0;

            // Magic
            MagicBytes.CopyTo(header.Slice(offset));
            offset += MagicBytes.Length;

            // Header Length (includes magic length like AesGcmKeyHeader does)
            BitConverter.TryWriteBytes(header.Slice(offset), headerLength);
            offset += sizeof(int);

            // Data Length
            BitConverter.TryWriteBytes(header.Slice(offset), (long)dataLength);
            offset += sizeof(long);

            // KeyId
            BitConverter.TryWriteBytes(header.Slice(offset), keyId);
            offset += sizeof(int);

            // Nonce
            nonce.CopyTo(header.Slice(offset));
            offset += NonceSize;

            // Tag
            tag.CopyTo(header.Slice(offset));
            offset += TagSize;

            output.Write(header);
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
        private readonly record struct EncryptionResult(int Index, byte[] Nonce, byte[] Tag, byte[] Data, int DataLength);

        private readonly record struct DecryptionJob(int Index, byte[] Nonce, byte[] Tag, byte[] Cipher, int DataLength);
        private readonly record struct DecryptionResult(int Index, byte[] Data, int DataLength);
    }
}
