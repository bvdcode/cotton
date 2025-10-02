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
                        // write chunk header with nonce included for compatibility
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
                            throw new InvalidDataException("Invalid chunk length in encrypted file.");
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
