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
                await DecryptParallelAsync(input, output, fileKey.AsSpan(0, KeySize).ToArray(), ct).ConfigureAwait(false);
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
                byte[] fileKeyArray = new byte[KeySize];
                Array.Copy(fileKey, fileKeyArray, KeySize);
                await EncryptParallelAsync(input, output, fileKeyArray, chunkSize, remainingLength, ct).ConfigureAwait(false);
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
                    byte[] readBuffer = BufferPool.Rent(chunkSize);
                    try
                    {
                        int bytesRead;
                        while ((bytesRead = input.Read(readBuffer, 0, chunkSize)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();
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

            var workerTasks = new List<Task>();
            for (int i = 0; i < ConcurrencyLevel; i++)
            {
                workerTasks.Add(Task.Run(async () =>
                {
                    using var workerGcm = new AesGcm(fileKey, TagSize);
                    await foreach (var job in reader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] nonce = new byte[NonceSize];
                        byte[] tag = new byte[TagSize];
                        RandomNumberGenerator.Fill(nonce);
                        byte[] encryptedBuffer = BufferPool.Rent(job.DataLength);
                        try
                        {
                            workerGcm.Encrypt(nonce, job.Data.AsSpan(0, job.DataLength), encryptedBuffer.AsSpan(0, job.DataLength), tag);
                            var result = new EncryptionResult(job.Index, nonce, tag, encryptedBuffer, job.DataLength);
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
                var pendingResults = new Dictionary<int, EncryptionResult>();
                int expectedIndex = 0;
                await foreach (var result in resultsReader.ReadAllAsync(ct))
                {
                    pendingResults[result.Index] = result;
                    while (pendingResults.TryGetValue(expectedIndex, out var nextResult))
                    {
                        try
                        {
                            WriteChunkHeader(output, _keyId, nextResult.Nonce, nextResult.Tag, nextResult.DataLength);
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

        private async Task DecryptParallelAsync(Stream input, Stream output, byte[] fileKey, CancellationToken ct)
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
                    while (!ct.IsCancellationRequested)
                    {
                        if (input.CanSeek && input.Position >= input.Length)
                            break;
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

        private static void WriteChunkHeader(Stream output, int keyId, byte[] nonce, byte[] tag, int dataLength)
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
