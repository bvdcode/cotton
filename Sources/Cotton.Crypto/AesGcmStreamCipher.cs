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
        /// Key identifier associated with the master key used to wrap file keys and build AAD.
        /// </summary>
        private readonly int _keyId;

        /// <summary>
        /// Raw master key bytes (32 bytes) used to wrap / unwrap per-file content encryption keys.
        /// </summary>
        private readonly byte[] _masterKeyBytes;

        /// <summary>
        /// AES-GCM nonce size in bytes (96-bit IV as recommended for GCM).
        /// </summary>
        public const int NonceSize = 12;

        /// <summary>
        /// AES-GCM authentication tag size in bytes (128-bit tag).
        /// </summary>
        public const int TagSize = 16;

        /// <summary>
        /// AES-256 key size in bytes.
        /// </summary>
        public const int KeySize = 32;

        /// <summary>
        /// Minimum allowed chunk size (64 KiB) to balance overhead and throughput.
        /// </summary>
        public const int MinChunkSize = 64 * 1024;

        /// <summary>
        /// Maximum allowed chunk size (64 MiB) to avoid excessive single-buffer allocations.
        /// </summary>
        public const int MaxChunkSize = 64 * 1024 * 1024;

        /// <summary>
        /// Default chunk size (16 MiB) chosen for high sequential throughput without large memory spikes.
        /// </summary>
        public const int DefaultChunkSize = 16 * 1024 * 1024;

        /// <summary>
        /// Shared <see cref="ArrayPool{T}"/> for renting / returning temporary buffers.
        /// </summary>
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

        /// <summary>
        /// Degree of parallelism for worker tasks. Initialized from CPU count or user override.
        /// </summary>
        private readonly int ConcurrencyLevel = Math.Min(4, Environment.ProcessorCount);

        /// <summary>
        /// Initializes a new instance of the <see cref="AesGcmStreamCipher"/> class.
        /// </summary>
        /// <param name="masterKey">32-byte master key used to wrap per-file keys.</param>
        /// <param name="keyId">Positive integer identifying the master key (embedded in headers / AAD).</param>
        /// <param name="threads">Optional override for parallel worker count (must be &gt; 0).</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="masterKey"/> is not 32 bytes.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="keyId"/> is not positive.</exception>
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
        /// Encrypts data from a readable <paramref name="input"/> stream and writes the encrypted
        /// stream (file header + chunk sequence) to a writable <paramref name="output"/> stream.
        /// </summary>
        /// <param name="input">Readable plaintext source stream.</param>
        /// <param name="output">Writable destination stream for encrypted bytes.</param>
        /// <param name="chunkSize">Chunk size in bytes (validated between <see cref="MinChunkSize"/> and <see cref="MaxChunkSize"/>).</param>
        /// <param name="ct">Cancellation token to abort the operation.</param>
        /// <returns>A task representing the asynchronous encryption pipeline.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="input"/> or <paramref name="output"/> is null.</exception>
        /// <exception cref="ArgumentException">If streams do not support required capabilities.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="chunkSize"/> is outside valid bounds.</exception>
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
        /// Decrypts an encrypted stream produced by <see cref="EncryptAsync"/> and writes plaintext
        /// bytes to the provided <paramref name="output"/> stream.
        /// </summary>
        /// <param name="input">Readable encrypted stream (position at file header).</param>
        /// <param name="output">Writable plaintext destination stream.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous decryption pipeline.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="input"/> or <paramref name="output"/> is null.</exception>
        /// <exception cref="ArgumentException">If streams do not support required capabilities.</exception>
        /// <exception cref="InvalidDataException">If file header key id mismatches this instance.</exception>
        /// <exception cref="CryptographicException">If authentication fails (e.g., modified header or key wrap failure).</exception>
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
        /// Runs the parallel encryption pipeline (producer -> workers -> consumer).
        /// </summary>
        /// <param name="input">Plaintext source stream positioned at first byte.</param>
        /// <param name="output">Encrypted destination stream (header already written).</param>
        /// <param name="fileKey">Unwrapped per-file key (rented buffer content).</param>
        /// <param name="chunkSize">Chunk size used for segmentation.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when all chunks are written.</returns>
        /// <remarks>
        /// Ensures output ordering via a reordering buffer while allowing workers to complete out of order.
        /// </remarks>
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
                    byte[] aad = new byte[32];
                    await foreach (EncryptionJob job in jobReader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] cipherBuffer = BufferPool.Rent(job.DataLength);
                        byte[] tagOwned = BufferPool.Rent(TagSize);
                        try
                        {
                            AesGcmStreamFormat.ComposeNonce(nonceBuffer, _keyId, job.Index);
                            AesGcmStreamFormat.BuildChunkAad(aad, _keyId, job.Index, job.DataLength);
                            gcm.Encrypt(nonceBuffer, job.DataBuffer.AsSpan(0, job.DataLength), cipherBuffer.AsSpan(0, job.DataLength), tagOwned.AsSpan(0, TagSize), aad);
                            await resultWriter.WriteAsync(new EncryptionResult(job.Index, tagOwned, cipherBuffer, job.DataLength), ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            BufferPool.Return(cipherBuffer);
                            BufferPool.Return(tagOwned);
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
                        Span<byte> nonce = stackalloc byte[NonceSize];
                        AesGcmStreamFormat.ComposeNonce(nonce, _keyId, result.Index);
                        AesGcmStreamFormat.WriteChunkHeader(output, _keyId, result.Index, nonce, result.Tag.AsSpan(0, TagSize), result.DataLength, NonceSize, TagSize);
                        await output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        BufferPool.Return(result.Tag);
                        BufferPool.Return(result.Data);
                        nextToWrite++;
                        while (waiting.TryGetValue(nextToWrite, out EncryptionResult nextRes))
                        {
                            waiting.Remove(nextToWrite);
                            Span<byte> nonce2 = stackalloc byte[NonceSize];
                            AesGcmStreamFormat.ComposeNonce(nonce2, _keyId, nextRes.Index);
                            AesGcmStreamFormat.WriteChunkHeader(output, _keyId, nextRes.Index, nonce2, nextRes.Tag.AsSpan(0, TagSize), nextRes.DataLength, NonceSize, TagSize);
                            await output.WriteAsync(nextRes.Data.AsMemory(0, nextRes.DataLength), ct).ConfigureAwait(false);
                            BufferPool.Return(nextRes.Tag);
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
        /// Creates bounded channels used for the decryption pipeline (jobs and results).
        /// </summary>
        /// <returns>Tuple containing the job channel and result channel.</returns>
        private (Channel<DecryptionJob> jobChannel, Channel<DecryptionResult> resultChannel) CreateDecryptionChannels()
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
            return (jobChannel, resultChannel);
        }

        /// <summary>
        /// Starts the producer that reads chunk headers and ciphertext from the encrypted stream and posts decryption jobs.
        /// </summary>
        /// <param name="input">Encrypted input stream (position at first chunk header).</param>
        /// <param name="writer">Channel writer for decryption jobs.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the producer loop.</returns>
        private Task StartDecryptionProducer(Stream input, ChannelWriter<DecryptionJob> writer, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                long chunkIndex = 0;
                try
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (input.CanSeek)
                        {
                            long bytesRemaining = input.Length - input.Position;
                            int minHeader = 4 + 4 + 8 + 4 + NonceSize + TagSize; // Conservative lower bound check
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
                            break; // graceful end
                        }
                        ValidateChunkHeader(chunkHeader, chunkIndex);

                        int cipherLength = (int)chunkHeader.PlaintextLength;
                        byte[] cipherBuffer = BufferPool.Rent(cipherLength);
                        await AesGcmStreamFormat.ReadExactlyAsync(input, cipherBuffer, cipherLength, ct).ConfigureAwait(false);
                        var job = new DecryptionJob(index: chunkIndex++, tag: chunkHeader.Tag, cipherBuffer: cipherBuffer, dataLength: cipherLength);
                        await writer.WriteAsync(job, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    writer.TryComplete();
                }
            }, ct);
        }

        /// <summary>
        /// Validates integrity of a chunk header before it is scheduled for decryption.
        /// </summary>
        /// <param name="header">Chunk header read from the stream.</param>
        /// <param name="expectedIndex">Expected sequential chunk index.</param>
        /// <exception cref="AuthenticationTagMismatchException">
        /// Thrown if any structural metadata (length, key id, nonce) does not match deterministic expectations.
        /// </exception>
        private void ValidateChunkHeader(ChunkHeader header, long expectedIndex)
        {
            if (header.PlaintextLength < 0 || header.PlaintextLength > MaxChunkSize)
                throw new AuthenticationTagMismatchException("Invalid chunk length in encrypted file.");
            if (header.KeyId != _keyId)
                throw new AuthenticationTagMismatchException("Chunk key ID mismatch.");
            Span<byte> expectedNonce = stackalloc byte[NonceSize];
            AesGcmStreamFormat.ComposeNonce(expectedNonce, _keyId, expectedIndex);
            if (!expectedNonce.SequenceEqual(header.Nonce))
                throw new AuthenticationTagMismatchException("Chunk nonce mismatch.");
        }

        /// <summary>
        /// Starts worker tasks that decrypt chunks in parallel and post results to the result channel.
        /// </summary>
        /// <param name="fileKey">Unwrapped file key.</param>
        /// <param name="jobReader">Reader for pending decryption jobs.</param>
        /// <param name="resultWriter">Writer for decrypted chunk payloads.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Array of started worker tasks.</returns>
        private Task[] StartDecryptionWorkers(byte[] fileKey, ChannelReader<DecryptionJob> jobReader, ChannelWriter<DecryptionResult> resultWriter, CancellationToken ct)
        {
            var workers = new Task[ConcurrencyLevel];
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workers[i] = Task.Run(async () =>
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
            return workers;
        }

        /// <summary>
        /// Starts the consumer that reorders decrypted results and writes them sequentially to the output stream.
        /// </summary>
        /// <param name="output">Plaintext output stream.</param>
        /// <param name="reader">Result channel reader.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when all results are flushed.</returns>
        private static Task StartDecryptionConsumer(Stream output, ChannelReader<DecryptionResult> reader, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                var waiting = new SortedDictionary<long, DecryptionResult>();
                long nextToWrite = 0;
                await foreach (DecryptionResult result in reader.ReadAllAsync(ct))
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
        }

        /// <summary>
        /// Orchestrates parallel decryption (producer, workers, consumer) for all remaining chunk records.
        /// </summary>
        /// <param name="input">Encrypted input stream positioned at first chunk header.</param>
        /// <param name="output">Writable plaintext stream.</param>
        /// <param name="fileKey">Unwrapped file key bytes.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when all chunks have been processed.</returns>
        private async Task DecryptChunksParallelAsync(Stream input, Stream output, byte[] fileKey, CancellationToken ct)
        {
            var (jobChannel, resultChannel) = CreateDecryptionChannels();
            var producer = StartDecryptionProducer(input, jobChannel.Writer, ct);
            var workers = StartDecryptionWorkers(fileKey, jobChannel.Reader, resultChannel.Writer, ct);
            var consumer = StartDecryptionConsumer(output, resultChannel.Reader, ct);

            try
            {
                await producer.ConfigureAwait(false);
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            finally
            {
                resultChannel.Writer.TryComplete();
            }

            await consumer.ConfigureAwait(false);
        }
    }
}
