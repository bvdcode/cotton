using System.Buffers;
using Cotton.Crypto.Internals;
using System.Threading.Channels;
using Cotton.Crypto.Abstractions;
using System.Security.Cryptography;

namespace Cotton.Crypto
{
    /// <summary>
    /// Provides high–throughput, chunked streaming encryption and decryption using AES-GCM.
    /// </summary>
    /// <remarks>
    /// Design:
    /// 1. A random per-file content encryption key (CEK) (file key) is generated for each encryption operation.
    /// 2. That file key is encrypted (wrapped) once with a long–lived master key via AES-GCM (file header).
    /// 3. Content is processed in fixed (or caller-specified) sized chunks (last chunk may be smaller).
    /// 4. Each chunk is encrypted under the file key with:
    ///    - Deterministically derived nonce: <c>ComposeNonce(masterKeyId, chunkIndex)</c>.
    ///    - AAD including key id, chunk index, and plaintext length for integrity binding.
    /// 5. Chunks are processed in parallel using a bounded channel pipeline:
    ///    - Producer reads plaintext -> jobs
    ///    - Workers encrypt/decrypt (parallel)
    ///    - Consumer reorders (if needed) and writes serialized chunk headers + payload.
    /// 
    /// Security Properties:
    /// - Nonce uniqueness: Guaranteed as long as <c>chunkIndex</c> does not repeat for the same master key / file key pair.
    ///   A per-file random key plus deterministic per-chunk nonces eliminates nonce reuse across files.
    /// - Integrity: AES-GCM tag per file key (header) and per chunk (chunk tag).
    /// - AAD binds structural metadata (key id, chunk index, plaintext length) preventing header tampering / cut-and-paste.
    /// 
    /// Limits:
    /// - Chunk index is <see cref="long"/>; practical limit is governed by memory / file size. Nonce derivation must not overflow.
    /// - Max chunk size is bounded to prevent pathological allocations.
    /// 
    /// Performance:
    /// - Uses <see cref="ArrayPool{T}"/> for buffer reuse.
    /// - Parallelism defaults to min(4, logical CPU count) unless overridden.
    /// - Ordering preserved by a reordering buffer (sorted dictionary) on the consumer side.
    /// 
    /// Thread Safety:
    /// - Instances are not thread-safe for concurrent encryption/decryption calls.
    /// - Each AES-GCM instance is local to a worker to avoid shared state.
    /// 
    /// Disposal:
    /// - The class does not own unmanaged resources; keys are zeroed when possible.
    /// </remarks>
    public class AesGcmStreamCipher : IStreamCipher
    {
        /// <summary>
        /// Logical identifier for the master key used to wrap per-file keys, enabling rotation / multi-key scenarios.
        /// </summary>
        private readonly int _keyId;

        /// <summary>
        /// Raw master key bytes (32 bytes). Copied from provided memory to avoid unintended aliasing.
        /// </summary>
        private readonly byte[] _masterKeyBytes;

        /// <summary>
        /// Size in bytes of per-chunk AES-GCM nonce (96 bits per NIST recommendation).
        /// </summary>
        public const int NonceSize = 12;

        /// <summary>
        /// Size in bytes of AES-GCM authentication tag (128 bits).
        /// </summary>
        public const int TagSize = 16;

        /// <summary>
        /// Size in bytes of the AES-256 key (file key and master key).
        /// </summary>
        public const int KeySize = 32;

        /// <summary>
        /// Minimum allowed chunk size (64 KiB).
        /// </summary>
        public const int MinChunkSize = 64 * 1024;

        /// <summary>
        /// Maximum allowed chunk size (64 MiB) to limit memory pressure.
        /// </summary>
        public const int MaxChunkSize = 64 * 1024 * 1024;

        /// <summary>
        /// Default chunk size (16 MiB) tuned for throughput / memory balance.
        /// </summary>
        public const int DefaultChunkSize = 16 * 1024 * 1024;

        /// <summary>
        /// Shared buffer pool for renting large arrays to reduce GC pressure.
        /// </summary>
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

        /// <summary>
        /// Degree of parallelism (worker count) used in the chunk pipeline.
        /// </summary>
        private readonly int ConcurrencyLevel = Math.Min(4, Environment.ProcessorCount);

        /// <summary>
        /// Creates a new streaming AES-GCM cipher instance.
        /// </summary>
        /// <param name="masterKey">32-byte master key (AES-256) used to wrap per-file keys.</param>
        /// <param name="keyId">Positive identifier associated with the master key.</param>
        /// <param name="threads">Optional override for parallel worker count (must be &gt; 0).</param>
        /// <exception cref="ArgumentException">If <paramref name="masterKey"/> is not 32 bytes.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="keyId"/> or <paramref name="threads"/> are invalid.</exception>
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

            _masterKeyBytes = masterKey.ToArray();
            _keyId = keyId;
            if (threads.HasValue && threads.Value > 0)
            {
                ConcurrencyLevel = threads.Value;
            }
        }

        /// <summary>
        /// Encrypts data from <paramref name="input"/> to <paramref name="output"/> in authenticated chunks.
        /// </summary>
        /// <param name="input">Readable stream containing plaintext.</param>
        /// <param name="output">Writable stream that receives ciphertext (with headers).</param>
        /// <param name="chunkSize">Chunk size in bytes (bounded by <see cref="MinChunkSize"/> and <see cref="MaxChunkSize"/>).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous encryption operation.</returns>
        /// <remarks>
        /// Output Format:
        /// [File Header] [Chunk Header + Chunk Ciphertext]*.
        /// Each chunk header includes nonce + tag + metadata; nonce also redundantly stored to aid external tooling.
        /// </remarks>
        /// <exception cref="ArgumentNullException">If streams are null.</exception>
        /// <exception cref="ArgumentException">If stream capabilities are insufficient.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="chunkSize"/> is invalid.</exception>
        /// <exception cref="OperationCanceledException">If cancelled.</exception>
        public async Task EncryptAsync(Stream input, Stream output, int chunkSize = DefaultChunkSize, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(output);
            if (!input.CanRead) throw new ArgumentException("Input stream must be readable.", nameof(input));
            if (!output.CanWrite) throw new ArgumentException("Output stream must be writable.", nameof(output));
            if (chunkSize < MinChunkSize || chunkSize > MaxChunkSize)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), $"Chunk size must be between {MinChunkSize} and {MaxChunkSize} bytes.");

            byte[] fileKey = BufferPool.Rent(KeySize);
            try
            {
                RandomNumberGenerator.Fill(fileKey.AsSpan(0, KeySize));
                byte[] fileKeyNonce = new byte[NonceSize];
                byte[] fileKeyTag = new byte[TagSize];
                RandomNumberGenerator.Fill(fileKeyNonce);
                byte[] encryptedFileKey = new byte[KeySize];
                using (var gcm = new AesGcm(_masterKeyBytes, TagSize))
                {
                    gcm.Encrypt(fileKeyNonce, fileKey.AsSpan(0, KeySize), encryptedFileKey, fileKeyTag);
                }

                long totalPlaintextLength = input.CanSeek ? Math.Max(0, input.Length - input.Position) : 0;
                AesGcmStreamFormat.WriteFileHeader(output, _keyId, fileKeyNonce, fileKeyTag, encryptedFileKey, totalPlaintextLength, NonceSize, TagSize, KeySize);

                await EncryptChunksParallelAsync(input, output, fileKey, chunkSize, ct).ConfigureAwait(false);
            }
            finally
            {
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
            }
        }

        /// <summary>
        /// Decrypts previously encrypted stream data produced by <see cref="EncryptAsync"/>
        /// </summary>
        /// <param name="input">Readable stream containing the encrypted file format.</param>
        /// <param name="output">Writable stream receiving plaintext.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous decryption operation.</returns>
        /// <exception cref="ArgumentNullException">If streams are null.</exception>
        /// <exception cref="ArgumentException">If stream capabilities are insufficient.</exception>
        /// <exception cref="InvalidDataException">If master key id mismatches file header.</exception>
        /// <exception cref="AuthenticationTagMismatchException">If integrity/authentication fails.</exception>
        /// <exception cref="OperationCanceledException">If cancelled.</exception>
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
                    gcm.Decrypt(header.Nonce, header.EncryptedKey, header.Tag, fileKey.AsSpan(0, KeySize));
                }
                await DecryptChunksParallelAsync(input, output, fileKey, ct).ConfigureAwait(false);
            }
            finally
            {
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
            }
        }

        /// <summary>
        /// Internal encryption pipeline: reads plaintext chunks, encrypts in parallel, writes ordered output.
        /// </summary>
        /// <param name="input">Plaintext source stream.</param>
        /// <param name="output">Ciphertext target stream.</param>
        /// <param name="fileKey">Per-file AES-256 key.</param>
        /// <param name="chunkSize">Chunk size.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <remarks>
        /// Uses two channels:
        /// - Job channel (plaintext chunks)
        /// - Result channel (ciphertext + tag)
        /// Reorders by chunk index before writing to ensure deterministic output order.
        /// </remarks>
        /// <exception cref="OperationCanceledException">If cancelled.</exception>
        private async Task EncryptChunksParallelAsync(Stream input, Stream output, byte[] fileKey, int chunkSize, CancellationToken ct)
        {
            var jobChannel = Channel.CreateBounded<EncryptionJob>(new BoundedChannelOptions(ConcurrencyLevel * 2)
            {
                SingleWriter = true,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            var resultChannel = Channel.CreateBounded<EncryptionResult>(new BoundedChannelOptions(ConcurrencyLevel * 2)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });

            var jobWriter = jobChannel.Writer;
            var jobReader = jobChannel.Reader;
            var resultWriter = resultChannel.Writer;
            var resultReader = resultChannel.Reader;

            long chunkIndex = 0;

            var producer = Task.Run(async () =>
            {
                try
                {
                    byte[]? buffer = null;
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        buffer = BufferPool.Rent(chunkSize);
                        int bytesRead = 0;
                        try
                        {
                            bytesRead = await input.ReadAsync(buffer.AsMemory(0, chunkSize), ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            if (buffer != null)
                            {
                                BufferPool.Return(buffer);
                                buffer = null;
                            }
                            throw;
                        }

                        if (bytesRead <= 0)
                        {
                            if (buffer != null)
                            {
                                BufferPool.Return(buffer);
                            }
                            break;
                        }

                        var job = new EncryptionJob(index: chunkIndex++, dataBuffer: buffer, dataLength: bytesRead);
                        await jobWriter.WriteAsync(job, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    jobWriter.TryComplete();
                }
            }, ct);

            var workerTasks = new Task[ConcurrencyLevel];
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workerTasks[i] = Task.Run(async () =>
                {
                    using var gcm = new AesGcm(fileKey, TagSize);
                    byte[] nonceBuffer = new byte[NonceSize];
                    byte[] tagBuffer = new byte[TagSize];
                    byte[] aad = new byte[32];
                    await foreach (EncryptionJob job in jobReader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] cipherBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            AesGcmStreamFormat.ComposeNonce(nonceBuffer, _keyId, job.Index);
                            AesGcmStreamFormat.BuildChunkAad(aad, _keyId, job.Index, job.DataLength);
                            gcm.Encrypt(nonceBuffer, job.DataBuffer.AsSpan(0, job.DataLength), cipherBuffer.AsSpan(0, job.DataLength), tagBuffer, aad);
                            await resultWriter.WriteAsync(new EncryptionResult(job.Index, [.. tagBuffer], cipherBuffer, job.DataLength), ct).ConfigureAwait(false);
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

            var consumer = Task.Run(async () =>
            {
                var waiting = new SortedDictionary<long, EncryptionResult>();
                long nextToWrite = 0;
                await foreach (EncryptionResult result in resultReader.ReadAllAsync(ct))
                {
                    if (result.Index == nextToWrite)
                    {
                        byte[] nonce = new byte[NonceSize];
                        AesGcmStreamFormat.ComposeNonce(nonce, _keyId, result.Index);
                        AesGcmStreamFormat.WriteChunkHeader(output, _keyId, result.Index, nonce, result.Tag, result.DataLength, NonceSize, TagSize);
                        await output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        BufferPool.Return(result.Data);
                        nextToWrite++;
                        while (waiting.TryGetValue(nextToWrite, out EncryptionResult nextRes))
                        {
                            waiting.Remove(nextToWrite);
                            byte[] nonce2 = new byte[NonceSize];
                            AesGcmStreamFormat.ComposeNonce(nonce2, _keyId, nextRes.Index);
                            AesGcmStreamFormat.WriteChunkHeader(output, _keyId, nextRes.Index, nonce2, nextRes.Tag, nextRes.DataLength, NonceSize, TagSize);
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
                    throw new InvalidDataException("Missing chunks in output ordering. File may be incomplete or corrupted.");
                }
            }, ct);

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

        /// <summary>
        /// Internal decryption pipeline: reads serialized chunk headers + ciphertext, decrypts in parallel, emits ordered plaintext.
        /// </summary>
        /// <param name="input">Ciphertext input stream (formatted by <see cref="EncryptAsync"/>).</param>
        /// <param name="output">Plaintext destination stream.</param>
        /// <param name="fileKey">Recovered per-file key.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <exception cref="AuthenticationTagMismatchException">If header or per-chunk integrity fails.</exception>
        /// <exception cref="InvalidDataException">If chunk ordering inconsistencies are detected at completion.</exception>
        /// <exception cref="EndOfStreamException">If truncated data is encountered mid-header.</exception>
        /// <exception cref="OperationCanceledException">If cancelled.</exception>
        private async Task DecryptChunksParallelAsync(Stream input, Stream output, byte[] fileKey, CancellationToken ct)
        {
            var jobChannel = Channel.CreateBounded<DecryptionJob>(new BoundedChannelOptions(ConcurrencyLevel * 2)
            {
                SingleWriter = true,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            var resultChannel = Channel.CreateBounded<DecryptionResult>(new BoundedChannelOptions(ConcurrencyLevel * 2)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });

            var jobWriter = jobChannel.Writer;
            var jobReader = jobChannel.Reader;
            var resultWriter = resultChannel.Writer;
            var resultReader = resultChannel.Reader;

            long chunkIndex = 0;

            var producer = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (input.CanSeek)
                        {
                            long bytesRemaining = input.Length - input.Position;
                            int minHeader = 4 + 4 + 8 + 4 + NonceSize + TagSize;
                            if (bytesRemaining == 0) break;
                            if (bytesRemaining < minHeader)
                                throw new EndOfStreamException("Unexpected end of stream while reading chunk header.");
                        }
                        ChunkHeader chunkHeader;
                        try
                        {
                            chunkHeader = await AesGcmStreamFormat.ReadChunkHeaderAsync(input, NonceSize, TagSize, ct).ConfigureAwait(false);
                        }
                        catch (EndOfStreamException)
                        {
                            break;
                        }
                        if (chunkHeader.PlaintextLength < 0 || chunkHeader.PlaintextLength > MaxChunkSize)
                        {
                            throw new AuthenticationTagMismatchException("Invalid chunk length in encrypted file.");
                        }
                        // Validate keyId in chunk header matches expected
                        if (chunkHeader.KeyId != _keyId)
                        {
                            throw new AuthenticationTagMismatchException("Chunk key ID mismatch.");
                        }
                        // Validate nonce in chunk header matches deterministic composition
                        byte[] expectedNonce = new byte[NonceSize];
                        AesGcmStreamFormat.ComposeNonce(expectedNonce, _keyId, chunkIndex);
                        if (!expectedNonce.AsSpan().SequenceEqual(chunkHeader.Nonce))
                        {
                            throw new AuthenticationTagMismatchException("Chunk nonce mismatch.");
                        }

                        int cipherLength = (int)chunkHeader.PlaintextLength;
                        byte[] cipherBuffer = BufferPool.Rent(cipherLength);
                        await AesGcmStreamFormat.ReadExactlyAsync(input, cipherBuffer, cipherLength, ct).ConfigureAwait(false);
                        var job = new DecryptionJob(index: chunkIndex++, tag: chunkHeader.Tag, cipherBuffer: cipherBuffer, dataLength: cipherLength);
                        await jobWriter.WriteAsync(job, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    jobWriter.TryComplete();
                }
            }, ct);

            var workerTasks = new Task[ConcurrencyLevel];
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workerTasks[i] = Task.Run(async () =>
                {
                    using var gcm = new AesGcm(fileKey, TagSize);
                    byte[] nonceBuffer = new byte[NonceSize];
                    byte[] aad = new byte[32];
                    await foreach (DecryptionJob job in jobReader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] plainBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            AesGcmStreamFormat.ComposeNonce(nonceBuffer, _keyId, job.Index);
                            AesGcmStreamFormat.BuildChunkAad(aad, _keyId, job.Index, job.DataLength);
                            gcm.Decrypt(nonceBuffer, job.Cipher.AsSpan(0, job.DataLength), job.Tag, plainBuffer.AsSpan(0, job.DataLength), aad);
                            var result = new DecryptionResult(job.Index, plainBuffer, job.DataLength);
                            await resultWriter.WriteAsync(result, ct).ConfigureAwait(false);
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
                }, ct);
            }

            var consumer = Task.Run(async () =>
            {
                var waiting = new SortedDictionary<long, DecryptionResult>();
                long nextToWrite = 0;
                await foreach (DecryptionResult result in resultReader.ReadAllAsync(ct))
                {
                    if (result.Index == nextToWrite)
                    {
                        await output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        BufferPool.Return(result.Data);
                        nextToWrite++;
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
