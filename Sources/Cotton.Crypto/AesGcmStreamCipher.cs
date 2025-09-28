using System.Buffers;
using Cotton.Crypto.Models;
using Cotton.Crypto.Helpers;
using Cotton.Crypto.Abstractions;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Buffers.Binary;

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
        private const int ReorderWindow = 256; // fixed-size window to avoid dictionary allocations

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
                throw new ArgumentException("Input stream must be readable.", nameof(input));
            if (!output.CanWrite)
                throw new ArgumentException("Output stream must be writable.", nameof(output));
            AesGcmKeyHeader keyHeader = AesGcmKeyHeader.FromStream(input, NonceSize, TagSize);
            byte[] fileKey = BufferPool.Rent(KeySize);
            try
            {
                using (AesGcm gcm = new(_masterKey.Span, TagSize))
                {
                    gcm.Decrypt(keyHeader.Nonce, keyHeader.EncryptedKey, keyHeader.Tag, fileKey.AsSpan(0, KeySize));
                }
                // Pass the pooled fileKey directly (no ToArray clone)
                await DecryptParallelAsync(input, output, fileKey, ct).ConfigureAwait(false);
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
                throw new ArgumentException("Input stream must be readable.", nameof(input));
            if (!output.CanWrite)
                throw new ArgumentException("Output stream must be writable.", nameof(output));
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
                using (AesGcm gcm = new(_masterKey.Span, TagSize))
                {
                    gcm.Encrypt(fileKeyNonce, fileKey.AsSpan(0, KeySize), encryptedFileKey, fileKeyTag);
                }
                long remainingLength = 0;
                if (input.CanSeek)
                {
                    remainingLength = Math.Max(0, input.Length - input.Position);
                }
                WriteFileHeader(output, _keyId, fileKeyNonce, fileKeyTag, encryptedFileKey, remainingLength);
                // Pass the pooled fileKey directly (no extra copy)
                await EncryptParallelAsync(input, output, fileKey, chunkSize, remainingLength, ct).ConfigureAwait(false);
            }
            finally
            {
                Array.Clear(fileKey, 0, KeySize);
                BufferPool.Return(fileKey);
            }
        }

        private async Task EncryptParallelAsync(Stream input, Stream output, byte[] fileKey, int chunkSize, long totalSize, CancellationToken ct)
        {
            var channel = Channel.CreateBounded<EncryptionJob>(ConcurrencyLevel * 2);
            var writer = channel.Writer;
            var reader = channel.Reader;

            var resultsChannel = Channel.CreateBounded<EncryptionResult>(ConcurrencyLevel * 2);
            var resultsWriter = resultsChannel.Writer;
            var resultsReader = resultsChannel.Reader;

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    int chunkIndex = 0;
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] buffer = BufferPool.Rent(chunkSize);
                        int bytesRead = 0;
                        try
                        {
                            bytesRead = await input.ReadAsync(buffer.AsMemory(0, chunkSize), ct).ConfigureAwait(false);
                            if (bytesRead <= 0)
                            {
                                BufferPool.Return(buffer);
                                break;
                            }
                            var job = new EncryptionJob(chunkIndex++, buffer, bytesRead);
                            await writer.WriteAsync(job, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            if (bytesRead <= 0 && buffer is not null)
                            {
                                BufferPool.Return(buffer);
                            }
                            throw;
                        }
                    }
                }
                finally
                {
                    writer.Complete();
                }
            }, ct);

            var workerTasks = new List<Task>();
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workerTasks.Add(Task.Run(async () =>
                {
                    using var workerGcm = new AesGcm(fileKey, TagSize);
                    Span<byte> nonce = stackalloc byte[NonceSize];
                    Span<byte> tagBytes = stackalloc byte[TagSize];
                    await foreach (var job in reader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] encryptedBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            // Deterministic nonce: 32-bit keyId prefix + 64-bit LE chunk index
                            ComposeNonce(nonce, _keyId, job.Index);

                            // Tag in stackalloc, then store as value type
                            workerGcm.Encrypt(nonce, job.Data.AsSpan(0, job.DataLength), encryptedBuffer.AsSpan(0, job.DataLength), tagBytes);

                            var tag = new Tag128(
                                BinaryPrimitives.ReadUInt64LittleEndian(tagBytes),
                                BinaryPrimitives.ReadUInt64LittleEndian(tagBytes[8..])
                            );

                            var result = new EncryptionResult(job.Index, tag, encryptedBuffer, job.DataLength);
                            await resultsWriter.WriteAsync(result, ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            BufferPool.Return(encryptedBuffer);
                            throw;
                        }
                        finally
                        {
                            BufferPool.Return(job.Data);
                        }
                    }
                }, ct));
            }

            var consumerTask = Task.Run(async () =>
            {
                // Fixed-size reordering window to avoid dictionary/hash overhead
                var window = new EncryptionResult[ReorderWindow];
                var present = new bool[ReorderWindow];
                int expectedIndex = 0;

                Span<byte> nonce = stackalloc byte[NonceSize];
                Span<byte> tagBytes = stackalloc byte[TagSize];
                await foreach (var result in resultsReader.ReadAllAsync(ct))
                {
                    int slot = result.Index % ReorderWindow;
                    window[slot] = result;
                    present[slot] = true;

                    // Drain in order while available
                    while (true)
                    {
                        int expectedSlot = expectedIndex % ReorderWindow;
                        if (!present[expectedSlot]) break;
                        var nextResult = window[expectedSlot];
                        if (nextResult.Index != expectedIndex) break; // slot holds a different cycle; wait
                        try
                        {
                            // Reconstruct nonce deterministically
                            ComposeNonce(nonce, _keyId, expectedIndex);

                            // Materialize tag bytes on stack
                            BinaryPrimitives.WriteUInt64LittleEndian(tagBytes, nextResult.Tag.Lo);
                            BinaryPrimitives.WriteUInt64LittleEndian(tagBytes[8..], nextResult.Tag.Hi);

                            WriteChunkHeader(output, _keyId, nonce, tagBytes, nextResult.DataLength);
                            await output.WriteAsync(nextResult.Data.AsMemory(0, nextResult.DataLength), ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            BufferPool.Return(nextResult.Data);
                            present[expectedSlot] = false;
                        }
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

        private static async Task DecryptParallelAsync(Stream input, Stream output, byte[] fileKey, CancellationToken ct)
        {
            var channel = Channel.CreateBounded<DecryptionJob>(ConcurrencyLevel * 2);
            var writer = channel.Writer;
            var reader = channel.Reader;

            var resultsChannel = Channel.CreateBounded<DecryptionResult>(ConcurrencyLevel * 2);
            var resultsWriter = resultsChannel.Writer;
            var resultsReader = resultsChannel.Reader;

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    int chunkIndex = 0;
                    while (true)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }
                        // Check if enough bytes remain for a valid header
                        if (input.CanSeek)
                        {
                            long bytesLeft = input.Length - input.Position;
                            int minHeaderSize = 4 + 4 + 8 + 4 + NonceSize + TagSize;
                            if (bytesLeft < minHeaderSize)
                            {
                                break;
                            }
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
                            workerGcm.Decrypt(job.Nonce, job.Cipher.AsSpan(0, job.DataLength), job.Tag, plainBuffer.AsSpan(default, job.DataLength));
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

            var consumerTask = Task.Run(async () =>
            {
                // Fixed-size reordering window for decryption as well
                var window = new DecryptionResult[ReorderWindow];
                var present = new bool[ReorderWindow];
                int expectedIndex = 0;
                await foreach (var result in resultsReader.ReadAllAsync(ct))
                {
                    int slot = result.Index % ReorderWindow;
                    window[slot] = result;
                    present[slot] = true;

                    while (true)
                    {
                        int expectedSlot = expectedIndex % ReorderWindow;
                        if (!present[expectedSlot]) break;
                        var nextResult = window[expectedSlot];
                        if (nextResult.Index != expectedIndex) break;
                        try
                        {
                            await output.WriteAsync(nextResult.Data.AsMemory(0, nextResult.DataLength), ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            BufferPool.Return(nextResult.Data);
                            present[expectedSlot] = false;
                        }
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

        private static void WriteFileHeader(Stream output, int keyId, byte[] nonce, byte[] tag, byte[] encryptedKey, long dataLength)
        {
            const int headerLength = 4 + 4 + 8 + 4 + NonceSize + TagSize + KeySize;
            Span<byte> header = stackalloc byte[headerLength];
            int offset = 0;
            MagicBytes.CopyTo(header[offset..]);
            offset += MagicBytes.Length;
            BitConverter.TryWriteBytes(header[offset..], headerLength);
            offset += sizeof(int);
            BitConverter.TryWriteBytes(header[offset..], dataLength);
            offset += sizeof(long);
            BitConverter.TryWriteBytes(header[offset..], keyId);
            offset += sizeof(int);
            nonce.CopyTo(header[offset..]);
            offset += NonceSize;
            tag.CopyTo(header[offset..]);
            offset += TagSize;
            encryptedKey.CopyTo(header[offset..]);
            output.Write(header);
        }

        private static void WriteChunkHeader(Stream output, int keyId, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, int dataLength)
        {
            const int headerLength = 4 + 4 + 8 + 4 + NonceSize + TagSize;
            Span<byte> header = stackalloc byte[headerLength];
            int offset = 0;
            MagicBytes.CopyTo(header[offset..]);
            offset += MagicBytes.Length;
            BitConverter.TryWriteBytes(header[offset..], headerLength);
            offset += sizeof(int);
            BitConverter.TryWriteBytes(header[offset..], (long)dataLength);
            offset += sizeof(long);
            BitConverter.TryWriteBytes(header[offset..], keyId);
            offset += sizeof(int);
            nonce.CopyTo(header[offset..]);
            offset += NonceSize;
            tag.CopyTo(header[offset..]);
            output.Write(header);
        }

        private static void WriteChunkHeader(Stream output, int keyId, byte[] nonce, byte[] tag, int dataLength)
        {
            // Backward-compatible overload
            WriteChunkHeader(output, keyId, (ReadOnlySpan<byte>)nonce, (ReadOnlySpan<byte>)tag, dataLength);
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

        private static void ComposeNonce(Span<byte> destination, int keyId, long chunkIndex)
        {
            // 32-bit prefix from keyId + 64-bit little-endian chunk index
            BinaryPrimitives.WriteUInt32LittleEndian(destination, unchecked((uint)keyId));
            BinaryPrimitives.WriteUInt64LittleEndian(destination[4..], unchecked((ulong)chunkIndex));
        }

        private readonly record struct EncryptionJob(int Index, byte[] Data, int DataLength);
        private readonly record struct Tag128(ulong Lo, ulong Hi);
        private readonly record struct EncryptionResult(int Index, Tag128 Tag, byte[] Data, int DataLength);
        private readonly record struct DecryptionJob(int Index, byte[] Nonce, byte[] Tag, byte[] Cipher, int DataLength);
        private readonly record struct DecryptionResult(int Index, byte[] Data, int DataLength);
    }
}
