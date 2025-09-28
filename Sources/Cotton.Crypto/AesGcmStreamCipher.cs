using System.Buffers;
using System.Buffers.Binary;
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
        public const int MinChunkSize = 64 * 1024;      // 64 KB
        public const int MaxChunkSize = 64 * 1024 * 1024; // 64 MB
        public const int DefaultChunkSize = 24 * 1024 * 1024; // 24 MB (default)

        // Magic header marker
        private static ReadOnlySpan<byte> MagicBytes => "CTN1"u8;
        // Shared buffer pool to reuse byte arrays and reduce GC pressure
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;
        // Concurrency level (number of parallel workers)
        private readonly int ConcurrencyLevel = Math.Max(2, Environment.ProcessorCount);

        public AesGcmStreamCipher(ReadOnlyMemory<byte> masterKey, int keyId = 1, int? threads = null)
        {
            if (masterKey.Length != KeySize)
                throw new ArgumentException($"Master key must be {KeySize} bytes ({KeySize * 8} bits) long.", nameof(masterKey));
            if (keyId <= 0)
                throw new ArgumentOutOfRangeException(nameof(keyId), "Key ID must be a positive integer.");

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
                WriteFileHeader(output, _keyId, fileKeyNonce, fileKeyTag, encryptedFileKey, totalPlaintextLength);

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
            FileHeader header = await ReadFileHeaderAsync(input, ct).ConfigureAwait(false);
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
                    await foreach (EncryptionJob job in jobReader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] cipherBuffer = BufferPool.Rent(job.DataLength);
                        EncryptionResult result;
                        try
                        {
                            // All stackalloc and crypto logic in a local block before any await
                            Span<byte> nonceSpan = stackalloc byte[NonceSize];
                            Span<byte> tagSpan = stackalloc byte[TagSize];
                            {
                                ComposeNonce(nonceSpan, _keyId, job.Index);
                                gcm.Encrypt(nonceSpan, job.DataBuffer.AsSpan(0, job.DataLength), cipherBuffer.AsSpan(0, job.DataLength), tagSpan);
                                var tagStruct = new Tag128(BitConverter.ToUInt64(tagSpan[..8]), BitConverter.ToUInt64(tagSpan.Slice(8, 8)));
                                result = new EncryptionResult(job.Index, tagStruct, cipherBuffer, job.DataLength);
                            }
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
                        WriteChunkHeader(output, _keyId, result.Index, result.Tag, result.DataLength);
                        await output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        // Return cipher buffer after writing
                        BufferPool.Return(result.Data);
                        nextToWrite++;
                        // Write any subsequently ready chunks from the waiting buffer
                        while (waiting.TryGetValue(nextToWrite, out EncryptionResult nextRes))
                        {
                            waiting.Remove(nextToWrite);
                            WriteChunkHeader(output, _keyId, nextRes.Index, nextRes.Tag, nextRes.DataLength);
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
                            if (bytesRemaining < (4 + 4 + 8 + 4 + NonceSize + TagSize))
                                break; // not enough bytes for another chunk header, end of file
                        }
                        // Read and parse the next chunk header
                        ChunkHeader chunkHeader;
                        try
                        {
                            chunkHeader = await ReadChunkHeaderAsync(input, ct).ConfigureAwait(false);
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
                        await ReadExactlyAsync(input, cipherBuffer, cipherLength, ct).ConfigureAwait(false);
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

        // Write the initial file header (metadata about file encryption)
        private void WriteFileHeader(Stream output, int keyId, byte[] fileKeyNonce, byte[] fileKeyTag, byte[] encryptedFileKey, long totalPlaintextLength)
        {
            // Calculate header length (includes magic + all header fields)
            const int HeaderLen = 4 + 4 + 8 + 4 + NonceSize + TagSize + KeySize;
            Span<byte> header = stackalloc byte[HeaderLen];
            int offset = 0;
            // Magic "CTN1"
            MagicBytes.CopyTo(header[offset..]);
            offset += MagicBytes.Length;                        // 4 bytes
            BitConverter.TryWriteBytes(header[offset..], HeaderLen);
            offset += sizeof(int);                              // 4 bytes (header length)
            BitConverter.TryWriteBytes(header[offset..], totalPlaintextLength);
            offset += sizeof(long);                             // 8 bytes (total plaintext length)
            BitConverter.TryWriteBytes(header[offset..], keyId);
            offset += sizeof(int);                              // 4 bytes (key ID)
            // Copy file key nonce (12 bytes) and tag (16 bytes)
            fileKeyNonce.CopyTo(header[offset..]);
            offset += NonceSize;
            fileKeyTag.CopyTo(header[offset..]);
            offset += TagSize;
            // Copy encrypted file key (32 bytes)
            encryptedFileKey.CopyTo(header[offset..]);
            // Write the header to output
            output.Write(header);
        }

        // Write a chunk header before each encrypted chunk in the output
        private void WriteChunkHeader(Stream output, int keyId, long chunkIndex, Tag128 tag, int plaintextLength)
        {
            // Each chunk header length (no encrypted key included)
            const int HeaderLen = 4 + 4 + 8 + 4 + NonceSize + TagSize;  // should equal 48
            Span<byte> header = stackalloc byte[HeaderLen];
            int offset = 0;
            MagicBytes.CopyTo(header[offset..]);
            offset += MagicBytes.Length;                        // 4 bytes
            BitConverter.TryWriteBytes(header[offset..], HeaderLen);
            offset += sizeof(int);                              // 4 bytes (header length)
            BitConverter.TryWriteBytes(header[offset..], (long)plaintextLength);
            offset += sizeof(long);                             // 8 bytes (ciphertext/plaintext length of chunk)
            BitConverter.TryWriteBytes(header[offset..], keyId);
            offset += sizeof(int);                              // 4 bytes (key ID)
            // Reconstruct the 12-byte nonce for this chunk (keyId + chunkIndex)
            Span<byte> nonceSpan = stackalloc byte[NonceSize];
            ComposeNonce(nonceSpan, keyId, chunkIndex);
            nonceSpan.CopyTo(header[offset..]);
            offset += NonceSize;
            // Write the 16-byte authentication tag from the Tag128 struct
            BinaryPrimitives.WriteUInt64LittleEndian(header[offset..], tag.Low);
            BinaryPrimitives.WriteUInt64LittleEndian(header[(offset + 8)..], tag.High);
            offset += TagSize;
            output.Write(header);
        }

        // Compose a 12-byte nonce (IV) for AES-GCM from a 4-byte keyId and 8-byte chunk index (both little-endian)
        private static void ComposeNonce(Span<byte> destination, int keyId, long chunkIndex)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, unchecked((uint)keyId));
            BinaryPrimitives.WriteUInt64LittleEndian(destination[4..], unchecked((ulong)chunkIndex));
        }

        // Parse the initial file header from input stream (magic, lengths, key info)
        private static async Task<FileHeader> ReadFileHeaderAsync(Stream input, CancellationToken ct)
        {
            // Read magic (4 bytes) and header length (4 bytes)
            byte[] headerPrefix = new byte[8];
            await ReadExactlyAsync(input, headerPrefix, 8, ct).ConfigureAwait(false);
            // Verify magic bytes
            if (!headerPrefix.AsSpan(0, 4).SequenceEqual(MagicBytes))
            {
                throw new InvalidDataException("Invalid file format: magic header not found.");
            }
            int headerLength = BitConverter.ToInt32(headerPrefix, 4);
            if (headerLength != (4 + 4 + 8 + 4 + NonceSize + TagSize + KeySize))
            {
                throw new InvalidDataException("Unsupported file header format (unexpected header length).");
            }
            // Read remaining header bytes (after the first 8 bytes we already read)
            int remainingHeader = headerLength - 8;
            byte[] headerData = new byte[remainingHeader];
            await ReadExactlyAsync(input, headerData, remainingHeader, ct).ConfigureAwait(false);
            // Parse file header fields
            long totalLength = BitConverter.ToInt64(headerData, 0);
            int keyId = BitConverter.ToInt32(headerData, 8);
            // Next 12 bytes: file key nonce
            byte[] nonce = new byte[NonceSize];
            Array.Copy(headerData, 12, nonce, 0, NonceSize);
            // Next 16 bytes: file key tag
            byte[] tag = new byte[TagSize];
            Array.Copy(headerData, 12 + NonceSize, tag, 0, TagSize);
            // Next 32 bytes: encrypted file key
            byte[] encryptedKey = new byte[KeySize];
            Array.Copy(headerData, 12 + NonceSize + TagSize, encryptedKey, 0, KeySize);
            return new FileHeader(totalLength, keyId, nonce, tag, encryptedKey);
        }

        // Parse a chunk header (48 bytes) for the next encrypted chunk
        private static async Task<ChunkHeader> ReadChunkHeaderAsync(Stream input, CancellationToken ct)
        {
            // Read magic + header length + plaintext length + keyId + nonce + tag (total 48 bytes expected)
            byte[] header = new byte[4 + 4 + 8 + 4 + NonceSize + TagSize];
            await ReadExactlyAsync(input, header, header.Length, ct).ConfigureAwait(false);
            // Verify magic
            if (!header.AsSpan(0, 4).SequenceEqual(MagicBytes))
            {
                throw new InvalidDataException("Chunk magic bytes missing or corrupted.");
            }
            int headerLength = BitConverter.ToInt32(header, 4);
            if (headerLength != header.Length)
            {
                throw new InvalidDataException("Invalid chunk header length.");
            }
            long plaintextLength = BitConverter.ToInt64(header, 8);
            int keyId = BitConverter.ToInt32(header, 16);
            // If keyId mismatches the file header's keyId, that's an error (we can optionally check this outside)
            byte[] nonce = new byte[NonceSize];
            Array.Copy(header, 20, nonce, 0, NonceSize);
            byte[] tag = new byte[TagSize];
            Array.Copy(header, 20 + NonceSize, tag, 0, TagSize);
            return new ChunkHeader(plaintextLength, keyId, nonce, tag);
        }

        // Read exactly 'count' bytes from stream into buffer or throw if unable to fill
        private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
        {
            int offset = 0;
            while (offset < count)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Unexpected end of stream.");
                }
                offset += bytesRead;
            }
        }

        // Structs to represent parsed headers and job/result data
        private readonly struct FileHeader(long totalLength, int keyId, byte[] nonce, byte[] tag, byte[] encryptedKey)
        {
            public long TotalPlaintextLength { get; } = totalLength;
            public int KeyId { get; } = keyId;
            public byte[] Nonce { get; } = nonce;
            public byte[] Tag { get; } = tag;
            public byte[] EncryptedKey { get; } = encryptedKey;
        }

        private readonly struct ChunkHeader(long length, int keyId, byte[] nonce, byte[] tag)
        {
            public long PlaintextLength { get; } = length;
            public int KeyId { get; } = keyId;
            public byte[] Nonce { get; } = nonce;
            public byte[] Tag { get; } = tag;
        }

        private readonly struct EncryptionJob(long index, byte[] dataBuffer, int dataLength)
        {
            public long Index { get; } = index;
            public byte[] DataBuffer { get; } = dataBuffer;
            public int DataLength { get; } = dataLength;
        }

        private readonly struct EncryptionResult(long index, AesGcmStreamCipher.Tag128 tag, byte[] data, int dataLength)
        {
            public long Index { get; } = index;
            public Tag128 Tag { get; } = tag;
            public byte[] Data { get; } = data;
            public int DataLength { get; } = dataLength;
        }

        private readonly struct DecryptionJob(long index, byte[] nonce, byte[] tag, byte[] cipherBuffer, int dataLength)
        {
            public long Index { get; } = index;
            public byte[] Nonce { get; } = nonce;
            public byte[] Tag { get; } = tag;
            public byte[] Cipher { get; } = cipherBuffer;
            public int DataLength { get; } = dataLength;
        }

        private readonly struct DecryptionResult(long index, byte[] data, int dataLength)
        {
            public long Index { get; } = index;
            public byte[] Data { get; } = data;
            public int DataLength { get; } = dataLength;
        }

        private readonly struct Tag128(ulong low, ulong high)
        {
            public ulong Low { get; } = low;
            public ulong High { get; } = high;
        }
    }
}
