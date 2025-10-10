using System.Buffers;
using System.Buffers.Binary;
using Cotton.Crypto.Internals;
using System.Threading.Channels;
using Cotton.Crypto.Abstractions;
using System.Security.Cryptography;

namespace Cotton.Crypto
{
    public class AesGcmStreamCipher : IStreamCipher
    {
        private readonly int _keyId;
        private readonly byte[] _masterKeyBytes;
        public const int NonceSize = 12;
        public const int TagSize = 16;
        public const int KeySize = 32;
        public const int MinChunkSize = 64 * 1024;
        public const int MaxChunkSize = 64 * 1024 * 1024;
        public const int DefaultChunkSize = 16 * 1024 * 1024;
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;
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
            if (!input.CanRead) throw new ArgumentException("Input stream must be readable.", nameof(input));
            if (!output.CanWrite) throw new ArgumentException("Output stream must be writable.", nameof(output));
            if (chunkSize < MinChunkSize || chunkSize > MaxChunkSize)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), $"Chunk size must be between {MinChunkSize} and {MaxChunkSize} bytes.");

            byte[] fileKey = BufferPool.Rent(KeySize);
            try
            {
                RandomNumberGenerator.Fill(fileKey.AsSpan(0, KeySize));
                // Per-file nonce prefix (4 bytes)
                Span<byte> prefixBytes = stackalloc byte[4];
                RandomNumberGenerator.Fill(prefixBytes);
                uint fileNoncePrefix = BinaryPrimitives.ReadUInt32LittleEndian(prefixBytes);

                byte[] fileKeyNonce = new byte[NonceSize];
                Tag128 fileKeyTag;
                RandomNumberGenerator.Fill(fileKeyNonce);
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

                await EncryptChunksParallelAsync(input, output, fileKey, fileNoncePrefix, chunkSize, ct).ConfigureAwait(false);
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
                await DecryptChunksParallelAsync(input, output, fileKey, header.NoncePrefix, ct).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(fileKey);
                BufferPool.Return(fileKey, clearArray: false);
            }
        }

        private async Task EncryptChunksParallelAsync(Stream input, Stream output, byte[] fileKey, uint fileNoncePrefix, int chunkSize, CancellationToken ct)
        {
            var jobChannel = Channel.CreateBounded<EncryptionJob>(new BoundedChannelOptions(ConcurrencyLevel * 4)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            var resultChannel = Channel.CreateBounded<EncryptionResult>(new BoundedChannelOptions(ConcurrencyLevel * 4)
            {
                SingleWriter = true,
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
                                BufferPool.Return(buffer, clearArray: false);
                                buffer = null;
                            }
                            throw;
                        }

                        if (bytesRead <= 0)
                        {
                            if (buffer != null)
                            {
                                BufferPool.Return(buffer, clearArray: false);
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
                    AesGcmStreamFormat.InitAadPrefix(aad, _keyId);
                    await foreach (EncryptionJob job in jobReader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] cipherBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            AesGcmStreamFormat.ComposeNonce(nonceBuffer, fileNoncePrefix, job.Index);
                            AesGcmStreamFormat.FillAadMutable(aad, job.Index, job.DataLength);
                            Span<byte> tagSpan = stackalloc byte[TagSize];
                            gcm.Encrypt(nonceBuffer, job.DataBuffer.AsSpan(0, job.DataLength), cipherBuffer.AsSpan(0, job.DataLength), tagSpan, aad);
                            var tag = Tag128.FromSpan(tagSpan);
                            await resultWriter.WriteAsync(new EncryptionResult(job.Index, tag, cipherBuffer, job.DataLength), ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            BufferPool.Return(cipherBuffer, clearArray: false);
                            throw;
                        }
                        finally
                        {
                            BufferPool.Return(job.DataBuffer, clearArray: false);
                        }
                    }
                }, ct);
            }

            var consumer = Task.Run(async () =>
            {
                int window = Math.Max(4, ConcurrencyLevel * 4);
                var ring = new EncryptionResult[window];
                var filled = new bool[window];
                var slotIndex = new long[window];
                long nextToWrite = 0;

                async Task FlushReadyAsync()
                {
                    while (true)
                    {
                        int slot = (int)(nextToWrite % window);
                        if (filled[slot] && slotIndex[slot] == nextToWrite)
                        {
                            var res = ring[slot];
                            int headerLen = AesGcmStreamFormat.ComputeChunkHeaderLength(TagSize);
                            byte[] headerBuf = BufferPool.Rent(headerLen);
                            try
                            {
                                AesGcmStreamFormat.BuildChunkHeader(headerBuf.AsSpan(0, headerLen), _keyId, res.Index, res.Tag, res.DataLength, TagSize);
                                await output.WriteAsync(headerBuf.AsMemory(0, headerLen), ct).ConfigureAwait(false);
                            }
                            finally
                            {
                                BufferPool.Return(headerBuf, clearArray: false);
                            }
                            await output.WriteAsync(res.Data.AsMemory(0, res.DataLength), ct).ConfigureAwait(false);
                            BufferPool.Return(res.Data, clearArray: false);
                            filled[slot] = false;
                            nextToWrite++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                await foreach (EncryptionResult result in resultReader.ReadAllAsync(ct))
                {
                    if (result.Index == nextToWrite)
                    {
                        int headerLen = AesGcmStreamFormat.ComputeChunkHeaderLength(TagSize);
                        byte[] headerBuf = BufferPool.Rent(headerLen);
                        try
                        {
                            AesGcmStreamFormat.BuildChunkHeader(headerBuf.AsSpan(0, headerLen), _keyId, result.Index, result.Tag, result.DataLength, TagSize);
                            await output.WriteAsync(headerBuf.AsMemory(0, headerLen), ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            BufferPool.Return(headerBuf, clearArray: false);
                        }
                        await output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        BufferPool.Return(result.Data, clearArray: false);
                        nextToWrite++;
                        await FlushReadyAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        long distance = result.Index - nextToWrite;
                        if (distance < 0 || distance >= window)
                        {
                            throw new InvalidDataException("Reordering window overflow or invalid chunk index.");
                        }
                        int slot = (int)(result.Index % window);
                        ring[slot] = result;
                        slotIndex[slot] = result.Index;
                        filled[slot] = true;
                    }
                }

                await FlushReadyAsync().ConfigureAwait(false);

                for (int i = 0; i < window; i++)
                {
                    if (filled[i])
                    {
                        BufferPool.Return(ring[i].Data, clearArray: false);
                        throw new InvalidDataException("Missing chunks in output ordering. File may be incomplete or corrupted.");
                    }
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

        private (Channel<DecryptionJob> jobChannel, Channel<DecryptionResult> resultChannel) CreateDecryptionChannels()
        {
            var jobChannel = Channel.CreateBounded<DecryptionJob>(new BoundedChannelOptions(ConcurrencyLevel * 4)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            var resultChannel = Channel.CreateBounded<DecryptionResult>(new BoundedChannelOptions(ConcurrencyLevel * 4)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            return (jobChannel, resultChannel);
        }

        private Task StartDecryptionProducer(Stream input, ChannelWriter<DecryptionJob> writer, uint fileNoncePrefix, CancellationToken ct)
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
                            int minHeader = 4 + 4 + 8 + 4 + TagSize;
                            if (bytesRemaining == 0) break;
                            if (bytesRemaining < minHeader)
                                throw new EndOfStreamException("Unexpected end of stream while reading chunk header.");
                        }
                        ChunkHeader chunkHeader;
                        try
                        {
                            chunkHeader = await AesGcmStreamFormat.ReadChunkHeaderAsync(input, TagSize, fileNoncePrefix, chunkIndex, ct).ConfigureAwait(false);
                        }
                        catch (EndOfStreamException)
                        {
                            break;
                        }

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

        private Task[] StartDecryptionWorkers(byte[] fileKey, ChannelReader<DecryptionJob> jobReader, ChannelWriter<DecryptionResult> resultWriter, uint fileNoncePrefix, CancellationToken ct)
        {
            var workers = new Task[ConcurrencyLevel];
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workers[i] = Task.Run(async () =>
                {
                    using var gcm = new AesGcm(fileKey, TagSize);
                    byte[] nonceBuffer = new byte[NonceSize];
                    byte[] aad = new byte[32];
                    AesGcmStreamFormat.InitAadPrefix(aad, _keyId);
                    await foreach (DecryptionJob job in jobReader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] plainBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            AesGcmStreamFormat.ComposeNonce(nonceBuffer, fileNoncePrefix, job.Index);
                            AesGcmStreamFormat.FillAadMutable(aad, job.Index, job.DataLength);
                            Span<byte> tagSpan = stackalloc byte[TagSize];
                            job.Tag.CopyTo(tagSpan);
                            gcm.Decrypt(nonceBuffer, job.Cipher.AsSpan(0, job.DataLength), tagSpan, plainBuffer.AsSpan(0, job.DataLength), aad);
                            var result = new DecryptionResult(job.Index, plainBuffer, job.DataLength);
                            await resultWriter.WriteAsync(result, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            BufferPool.Return(plainBuffer, clearArray: false);
                            throw;
                        }
                        finally
                        {
                            BufferPool.Return(job.Cipher, clearArray: false);
                        }
                    }
                }, ct);
            }
            return workers;
        }

        private static Task StartDecryptionConsumer(Stream output, ChannelReader<DecryptionResult> reader, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                const int minWindow = 4;
                int window = minWindow;
                var ring = new DecryptionResult[window];
                var filled = new bool[window];
                var slotIndex = new long[window];
                long nextToWrite = 0;

                void EnsureCapacity(long neededIndex)
                {
                    if (neededIndex - nextToWrite < window) return;
                    int newWindow = window * 2;
                    while (neededIndex - nextToWrite >= newWindow)
                        newWindow *= 2;
                    Array.Resize(ref ring, newWindow);
                    Array.Resize(ref filled, newWindow);
                    Array.Resize(ref slotIndex, newWindow);
                    window = newWindow;
                }

                async Task FlushReadyAsync()
                {
                    while (true)
                    {
                        int slot = (int)(nextToWrite % window);
                        if (filled[slot] && slotIndex[slot] == nextToWrite)
                        {
                            var res = ring[slot];
                            await output.WriteAsync(res.Data.AsMemory(0, res.DataLength), ct).ConfigureAwait(false);
                            BufferPool.Return(res.Data, clearArray: false);
                            filled[slot] = false;
                            nextToWrite++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                await foreach (DecryptionResult result in reader.ReadAllAsync(ct))
                {
                    if (result.Index == nextToWrite)
                    {
                        await output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        BufferPool.Return(result.Data, clearArray: false);
                        nextToWrite++;
                        await FlushReadyAsync();
                    }
                    else
                    {
                        if (result.Index < nextToWrite)
                        {
                            BufferPool.Return(result.Data, clearArray: false);
                            throw new InvalidDataException("Received duplicate or out-of-order chunk behind the write cursor.");
                        }
                        EnsureCapacity(result.Index);
                        int slot = (int)(result.Index % window);
                        ring[slot] = result;
                        slotIndex[slot] = result.Index;
                        filled[slot] = true;
                    }
                }

                await FlushReadyAsync();

                // Validate no leftovers remain
                for (int i = 0; i < window; i++)
                {
                    if (filled[i])
                    {
                        BufferPool.Return(ring[i].Data, clearArray: false);
                        throw new InvalidDataException("Decryption output missing chunks. The encrypted data may be incomplete or corrupted.");
                    }
                }
            }, ct);
        }

        private async Task DecryptChunksParallelAsync(Stream input, Stream output, byte[] fileKey, uint fileNoncePrefix, CancellationToken ct)
        {
            var (jobChannel, resultChannel) = CreateDecryptionChannels();
            var producer = StartDecryptionProducer(input, jobChannel.Writer, fileNoncePrefix, ct);
            var workers = StartDecryptionWorkers(fileKey, jobChannel.Reader, resultChannel.Writer, fileNoncePrefix, ct);
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
