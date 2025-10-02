using System.Buffers;
using Cotton.Crypto.Internals;
using System.Threading.Channels;
using Cotton.Crypto.Abstractions;
using System.Security.Cryptography;

namespace Cotton.Crypto
{
    public class AesGcmStreamCipher : IStreamCipher
    {
        private readonly int _keyId;
        private readonly byte[] _masterKeyBytes;  // Master key as byte array for AesGcm

        // Cryptographic constants
        public const int NonceSize = 12;    // 96-bit nonce for AES-GCM
        public const int TagSize = 16;      // 128-bit authentication tag
        public const int KeySize = 32;      // 256-bit key size (32 bytes)

        // Chunk size bounds (can be tuned for performance vs. memory)
        public const int MinChunkSize = 64 * 1024;              // 64 KB
        public const int MaxChunkSize = 64 * 1024 * 1024;       // 64 MB
        public const int DefaultChunkSize = 16 * 1024 * 1024;   // 16 MB (default)

        // Shared buffer pool to reuse byte arrays and reduce GC pressure
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;
        // Concurrency level (number of parallel workers)
        private readonly int ConcurrencyLevel = Math.Min(4, Environment.ProcessorCount);

        public AesGcmStreamCipher(ReadOnlyMemory<byte> masterKey, int keyId = 1, int? threads = null)
        {
            if (masterKey.Length != KeySize)
            {
                throw new ArgumentException($"Master key must be {KeySize} bytes ({KeySize * 8} bits) long.", nameof(masterKey));
            }
            if (keyId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(keyId), "Key ID must be a positive integer.");
            }    

            // Store master key as byte array for AesGcm usage
            _masterKeyBytes = masterKey.ToArray();
            _keyId = keyId;
            if (threads.HasValue && threads.Value > 0)
            {
                ConcurrencyLevel = threads.Value;
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
                throw new ArgumentOutOfRangeException(nameof(chunkSize), $"Chunk size must be between {MinChunkSize} and {MaxChunkSize} bytes.");
            }

            // Generate a random file-specific key (256-bit) and encrypt it with the master key
            byte[] fileKey = BufferPool.Rent(KeySize);
            try
            {
                RandomNumberGenerator.Fill(fileKey.AsSpan(0, KeySize));
                // Encrypt the fileKey using master key to produce an encrypted key and authentication tag
                byte[] fileKeyNonce = new byte[NonceSize];
                byte[] fileKeyTag = new byte[TagSize];
                RandomNumberGenerator.Fill(fileKeyNonce);
                byte[] encryptedFileKey = new byte[KeySize];
                using (var gcm = new AesGcm(_masterKeyBytes, TagSize))
                {
                    gcm.Encrypt(fileKeyNonce, fileKey.AsSpan(0, KeySize), encryptedFileKey, fileKeyTag);
                }

                // Determine total plaintext length if known (for header information)
                long totalPlaintextLength = 0;
                if (input.CanSeek)
                {
                    totalPlaintextLength = Math.Max(0, input.Length - input.Position);
                }

                // Write the file header (magic + header length + total length + keyId + fileKey nonce/tag + encrypted fileKey)
                AesGcmStreamFormat.WriteFileHeader(output, _keyId, fileKeyNonce, fileKeyTag, encryptedFileKey, totalPlaintextLength, NonceSize, TagSize, KeySize);

                // Encrypt data chunks in parallel using the generated fileKey
                await EncryptChunksParallelAsync(input, output, fileKey, chunkSize, ct).ConfigureAwait(false);
            }
            finally
            {
                // Zero out and return the fileKey buffer to pool
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
            }
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

            // Read and parse the file header to obtain encrypted file key and parameters
            FileHeader header = await AesGcmStreamFormat.ReadFileHeaderAsync(input, NonceSize, TagSize, KeySize, ct).ConfigureAwait(false);
            if (header.KeyId != _keyId)
            {
                throw new InvalidDataException($"Key ID mismatch. Expected {_keyId}, but file has {header.KeyId}.");
            }

            // Decrypt the file-specific key using the master key
            byte[] fileKey = BufferPool.Rent(KeySize);
            try
            {
                using (var gcm = new AesGcm(_masterKeyBytes, TagSize))
                {
                    // Decrypt the 32-byte encrypted file key to retrieve the actual file key
                    gcm.Decrypt(header.Nonce, header.EncryptedKey, header.Tag, fileKey.AsSpan(0, KeySize));
                }
                // Decrypt data chunks in parallel using the recovered fileKey
                await DecryptChunksParallelAsync(input, output, fileKey, ct).ConfigureAwait(false);
            }
            finally
            {
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
            }
        }

        private async Task EncryptChunksParallelAsync(Stream input, Stream output, byte[] fileKey, int chunkSize, CancellationToken ct)
        {
            // Create bounded channels for job queue and results, with capacity to buffer some chunks
            var jobChannel = Channel.CreateBounded<EncryptionJob>(new BoundedChannelOptions(ConcurrencyLevel * 2)
            {
                SingleWriter = true,   // only one producer reading from input
                SingleReader = false,  // multiple consumer workers
                FullMode = BoundedChannelFullMode.Wait
            });
            var resultChannel = Channel.CreateBounded<EncryptionResult>(new BoundedChannelOptions(ConcurrencyLevel * 2)
            {
                SingleWriter = false,  // multiple producers (worker tasks) writing results
                SingleReader = true,   // single consumer writing output
                FullMode = BoundedChannelFullMode.Wait
            });

            ChannelWriter<EncryptionJob> jobWriter = jobChannel.Writer;
            ChannelReader<EncryptionJob> jobReader = jobChannel.Reader;
            ChannelWriter<EncryptionResult> resultWriter = resultChannel.Writer;
            ChannelReader<EncryptionResult> resultReader = resultChannel.Reader;

            // Track next chunk index for ordering
            // Note: use long to avoid overflow for very large files
            long chunkIndex = 0;

            // Producer task: read input stream, partition into chunks, and enqueue encryption jobs
            var producer = Task.Run(async () =>
            {
                try
                {
                    byte[]? buffer = null;
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        // Rent a buffer for the chunk
                        buffer = BufferPool.Rent(chunkSize);
                        int bytesRead = 0;
                        try
                        {
                            // Read up to chunkSize bytes from input
                            bytesRead = await input.ReadAsync(buffer.AsMemory(0, chunkSize), ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            // If an exception occurs during read, ensure buffer is returned
                            if (buffer != null)
                            {
                                BufferPool.Return(buffer);
                                buffer = null;
                            }
                            throw;
                        }

                        if (bytesRead <= 0)
                        {
                            // Reached end of input stream
                            if (buffer != null)
                            {
                                BufferPool.Return(buffer);
                            }
                            break;
                        }

                        // Create a job for this chunk (includes index and the buffer + length)
                        var job = new EncryptionJob(index: chunkIndex++, dataBuffer: buffer, dataLength: bytesRead);
                        // Enqueue the job (will wait if channel is full, applying backpressure)
                        await jobWriter.WriteAsync(job, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    // Signal that no more jobs will be produced
                    jobWriter.TryComplete();
                }
            }, ct);

            // Worker tasks: encrypt chunks in parallel
            var workerTasks = new Task[ConcurrencyLevel];
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workerTasks[i] = Task.Run(async () =>
                {
                    using var gcm = new AesGcm(fileKey, TagSize);
                    // Allocate nonce and tag buffers once per worker, outside the loop
                    byte[] nonceBuffer = new byte[NonceSize];
                    byte[] tagBuffer = new byte[TagSize];
                    await foreach (EncryptionJob job in jobReader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] cipherBuffer = BufferPool.Rent(job.DataLength);
                        EncryptionResult result;
                        try
                        {
                            AesGcmStreamFormat.ComposeNonce(nonceBuffer, _keyId, job.Index);
                            gcm.Encrypt(nonceBuffer, job.DataBuffer.AsSpan(0, job.DataLength), cipherBuffer.AsSpan(0, job.DataLength), tagBuffer);
                            var tagStruct = new Tag128(BitConverter.ToUInt64(tagBuffer, 0), BitConverter.ToUInt64(tagBuffer, 8));
                            result = new EncryptionResult(job.Index, tagStruct, cipherBuffer, job.DataLength);
                            await resultWriter.WriteAsync(result, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            BufferPool.Return(cipherBuffer);
                            throw;
                        }
                        finally
                        {
                            BufferPool.Return(job.DataBuffer);
                        }
                    }
                }, ct);
            }

            // Consumer task: take encrypted results and write to output in order
            var consumer = Task.Run(async () =>
            {
                // Buffer for out-of-order results
                var waiting = new SortedDictionary<long, EncryptionResult>();
                long nextToWrite = 0;
                await foreach (EncryptionResult result in resultReader.ReadAllAsync(ct))
                {
                    if (result.Index == nextToWrite)
                    {
                        // This is the next expected chunk - write it immediately
                        AesGcmStreamFormat.WriteChunkHeader(output, _keyId, result.Index, result.Tag, result.DataLength, NonceSize, TagSize);
                        await output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        // Return cipher buffer after writing
                        BufferPool.Return(result.Data);
                        nextToWrite++;
                        // Write any subsequently ready chunks from the waiting buffer
                        while (waiting.TryGetValue(nextToWrite, out EncryptionResult nextRes))
                        {
                            waiting.Remove(nextToWrite);
                            AesGcmStreamFormat.WriteChunkHeader(output, _keyId, nextRes.Index, nextRes.Tag, nextRes.DataLength, NonceSize, TagSize);
                            await output.WriteAsync(nextRes.Data.AsMemory(0, nextRes.DataLength), ct).ConfigureAwait(false);
                            BufferPool.Return(nextRes.Data);
                            nextToWrite++;
                        }
                    }
                    else
                    {
                        // Out-of-order result, store it until its turn comes
                        waiting[result.Index] = result;
                    }
                }

                // After channel completion, ensure no leftover results waiting (should not happen if all chunks written)
                if (waiting.Count > 0)
                {
                    throw new InvalidDataException("Missing chunks in output ordering. File may be incomplete or corrupted.");
                }
            }, ct);

            // Wait for producer and all workers to finish, then complete the result channel
            try
            {
                await producer.ConfigureAwait(false);
                await Task.WhenAll(workerTasks).ConfigureAwait(false);
            }
            finally
            {
                // Signal that no more results will be produced
                resultWriter.TryComplete();
            }
            // Wait for the consumer to finish writing all data
            await consumer.ConfigureAwait(false);
        }

        private async Task DecryptChunksParallelAsync(Stream input, Stream output, byte[] fileKey, CancellationToken ct)
        {
            // Setup channels for decryption jobs and results
            var jobChannel = Channel.CreateBounded<DecryptionJob>(new BoundedChannelOptions(ConcurrencyLevel * 2)
            {
                SingleWriter = true,    // one producer reading input
                SingleReader = false,   // multiple decryption workers
                FullMode = BoundedChannelFullMode.Wait
            });
            var resultChannel = Channel.CreateBounded<DecryptionResult>(new BoundedChannelOptions(ConcurrencyLevel * 2)
            {
                SingleWriter = false,   // multiple producers (workers)
                SingleReader = true,    // one consumer writing output
                FullMode = BoundedChannelFullMode.Wait
            });

            var jobWriter = jobChannel.Writer;
            var jobReader = jobChannel.Reader;
            var resultWriter = resultChannel.Writer;
            var resultReader = resultChannel.Reader;

            long chunkIndex = 0;

            // Producer: read each chunk header + ciphertext from input and queue decryption jobs
            var producer = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        // If we can determine stream length, break if not enough for even a header
                        if (input.CanSeek)
                        {
                            long bytesRemaining = input.Length - input.Position;
                            int minHeader = 4 + 4 + 8 + 4 + NonceSize + TagSize;
                            if (bytesRemaining == 0)
                            {
                                break; // clean EOF
                            }    
                            if (bytesRemaining < minHeader)
                            {
                                throw new EndOfStreamException("Unexpected end of stream while reading chunk header.");
                            }
                        }
                        // Read and parse the next chunk header
                        ChunkHeader chunkHeader;
                        try
                        {
                            chunkHeader = await AesGcmStreamFormat.ReadChunkHeaderAsync(input, NonceSize, TagSize, ct).ConfigureAwait(false);
                        }
                        catch (EndOfStreamException)
                        {
                            // Cleanly handle EOF
                            break;
                        }
                        // Validate chunk length
                        if (chunkHeader.PlaintextLength < 0 || chunkHeader.PlaintextLength > MaxChunkSize)
                        {
                            throw new InvalidDataException("Invalid chunk length in encrypted file.");
                        }
                        // The chunk length in header indicates how many ciphertext bytes to read
                        int cipherLength = (int)chunkHeader.PlaintextLength;
                        byte[] cipherBuffer = BufferPool.Rent(cipherLength);
                        // Read the ciphertext bytes exactly
                        await AesGcmStreamFormat.ReadExactlyAsync(input, cipherBuffer, cipherLength, ct).ConfigureAwait(false);
                        // Enqueue the decryption job
                        var job = new DecryptionJob(index: chunkIndex++, nonce: chunkHeader.Nonce, tag: chunkHeader.Tag, cipherBuffer: cipherBuffer, dataLength: cipherLength);
                        await jobWriter.WriteAsync(job, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    jobWriter.TryComplete();
                }
            }, ct);

            // Worker tasks: perform decryption in parallel
            var workerTasks = new Task[ConcurrencyLevel];
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workerTasks[i] = Task.Run(async () =>
                {
                    using var gcm = new AesGcm(fileKey, TagSize);
                    await foreach (DecryptionJob job in jobReader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        // Rent buffer for plaintext output
                        byte[] plainBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            // Perform AES-GCM decryption; will throw if authentication fails
                            gcm.Decrypt(job.Nonce, job.Cipher.AsSpan(0, job.DataLength), job.Tag, plainBuffer.AsSpan(0, job.DataLength));
                            var result = new DecryptionResult(job.Index, plainBuffer, job.DataLength);
                            await resultWriter.WriteAsync(result, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            BufferPool.Return(plainBuffer);
                            throw; // propagate error (e.g., authentication tag mismatch)
                        }
                        finally
                        {
                            // Return cipher buffer to pool
                            BufferPool.Return(job.Cipher);
                        }
                    }
                }, ct);
            }

            // Consumer: write plaintext chunks in correct order
            var consumer = Task.Run(async () =>
            {
                var waiting = new SortedDictionary<long, DecryptionResult>();
                long nextToWrite = 0;
                await foreach (DecryptionResult result in resultReader.ReadAllAsync(ct))
                {
                    if (result.Index == nextToWrite)
                    {
                        // Write plaintext chunk directly to output
                        await output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        BufferPool.Return(result.Data);
                        nextToWrite++;
                        // Flush any buffered subsequent chunks that are now in order
                        while (waiting.TryGetValue(nextToWrite, out DecryptionResult nextRes))
                        {
                            waiting.Remove(nextToWrite);
                            await output.WriteAsync(nextRes.Data.AsMemory(0, nextRes.DataLength), ct).ConfigureAwait(false);
                            BufferPool.Return(nextRes.Data);
                            nextToWrite++;
                        }
                    }
                    else
                    {
                        waiting[result.Index] = result;
                    }
                }
                if (waiting.Count > 0)
                {
                    throw new InvalidDataException("Decryption output missing chunks. The encrypted data may be incomplete or corrupted.");
                }
            }, ct);

            // Coordinate completion
            try
            {
                await producer.ConfigureAwait(false);
                await Task.WhenAll(workerTasks).ConfigureAwait(false);
            }
            finally
            {
                resultWriter.TryComplete();
            }
            await consumer.ConfigureAwait(false);
        }
    }
}
