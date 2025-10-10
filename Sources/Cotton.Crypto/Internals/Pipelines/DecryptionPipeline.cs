using System.Buffers;
using System.Threading.Channels;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace Cotton.Crypto.Internals.Pipelines
{
    internal sealed class DecryptionPipeline
    {
        private readonly Stream _input;
        private readonly Stream _output;
        private readonly byte[] _fileKey;
        private readonly uint _noncePrefix;
        private readonly int _threads;
        private readonly int _keyId;
        private readonly int _nonceSize;
        private readonly int _tagSize;
        private readonly int _maxChunkSize;
        private readonly ArrayPool<byte> _pool;

        public DecryptionPipeline(Stream input, Stream output, byte[] fileKey, uint noncePrefix, int threads, int keyId, int nonceSize, int tagSize, int maxChunkSize, ArrayPool<byte> pool)
        {
            _input = input; _output = output; _fileKey = fileKey; _noncePrefix = noncePrefix; _threads = threads; _keyId = keyId;
            _nonceSize = nonceSize; _tagSize = tagSize; _maxChunkSize = maxChunkSize; _pool = pool;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var jobCh = Channel.CreateBounded<DecryptionJob>(new BoundedChannelOptions(_threads * 4) { SingleWriter = true, SingleReader = false, FullMode = BoundedChannelFullMode.Wait });
            var resCh = Channel.CreateBounded<DecryptionResult>(new BoundedChannelOptions(_threads * 4) { SingleWriter = false, SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

            int jobCap = _threads * 4;
            int resCap = _threads * 4;
            int window = Math.Max(4, _threads * 4);
            int maxCount = jobCap + _threads + resCap + window + 8;
            long maxBytes = (long)_maxChunkSize * maxCount * 4;
            using var scope = new BufferScope(_pool, maxCount: maxCount, maxBytes: maxBytes);

            var producer = ProduceAsync(jobCh.Writer, scope, ct);
            var workers = StartWorkersAsync(jobCh.Reader, resCh.Writer, scope, ct);
            var consumer = ConsumeAsync(resCh.Reader, scope, ct);

            try
            {
                await producer.ConfigureAwait(false);
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            finally
            {
                resCh.Writer.TryComplete();
            }

            await consumer.ConfigureAwait(false);
        }

        private Task ProduceAsync(ChannelWriter<DecryptionJob> writer, BufferScope scope, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                long idx = 0;
                try
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (_input.CanSeek)
                        {
                            long bytesRemaining = _input.Length - _input.Position;
                            int minHeader = 4 + 4 + 8 + 4 + _tagSize;
                            if (bytesRemaining == 0) break;
                            if (bytesRemaining < minHeader) throw new EndOfStreamException("Unexpected end of stream while reading chunk header.");
                        }
                        ChunkHeader chunkHeader;
                        try
                        {
                            chunkHeader = await AesGcmStreamFormat.ReadChunkHeaderAsync(_input, _tagSize, ct).ConfigureAwait(false);
                        }
                        catch (EndOfStreamException)
                        {
                            break;
                        }

                        if (chunkHeader.KeyId != _keyId) throw new InvalidDataException("Chunk key ID does not match file key ID.");
                        if (chunkHeader.PlaintextLength <= 0 || chunkHeader.PlaintextLength > _maxChunkSize) throw new InvalidDataException("Invalid chunk length in header.");
                        if (_input.CanSeek)
                        {
                            long remaining = _input.Length - _input.Position;
                            if (remaining < chunkHeader.PlaintextLength) throw new EndOfStreamException("Unexpected end of stream while reading chunk ciphertext.");
                        }

                        int cipherLength = (int)chunkHeader.PlaintextLength;
                        byte[] cipher = scope.Rent(cipherLength);
                        await AesGcmStreamFormat.ReadExactlyAsync(_input, cipher, cipherLength, ct).ConfigureAwait(false);
                        if (unchecked((ulong)idx) == ulong.MaxValue)
                        {
                            scope.Recycle(cipher);
                            throw new InvalidOperationException("Maximum number of chunks per file is 2^64-1. Counter reached ulong.MaxValue.");
                        }
                        await writer.WriteAsync(new DecryptionJob(idx++, chunkHeader.Tag, cipher, cipherLength), ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    writer.TryComplete();
                }
            }, ct);
        }

        private Task[] StartWorkersAsync(ChannelReader<DecryptionJob> reader, ChannelWriter<DecryptionResult> writer, BufferScope scope, CancellationToken ct)
        {
            var tasks = new Task[_threads];
            for (int i = 0; i < _threads; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using var gcm = new AesGcm(_fileKey, _tagSize);
                    byte[] nonceBuffer = new byte[_nonceSize];
                    byte[] aad = new byte[32];
                    AesGcmStreamFormat.InitAadPrefix(aad, _keyId);
                    await foreach (var job in reader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] plain = scope.Rent(job.DataLength);
                        try
                        {
                            AesGcmStreamFormat.ComposeNonce(nonceBuffer, _noncePrefix, job.Index);
                            AesGcmStreamFormat.FillAadMutable(aad, job.Index, job.DataLength);
                            Tag128 tagCopy = job.Tag; Span<byte> tagSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref tagCopy, 1));
                            gcm.Decrypt(nonceBuffer, job.Cipher.AsSpan(0, job.DataLength), tagSpan, plain.AsSpan(0, job.DataLength), aad);
                            await writer.WriteAsync(new DecryptionResult(job.Index, plain, job.DataLength), ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            scope.Recycle(job.Cipher);
                        }
                    }
                }, ct);
            }
            return tasks;
        }

        private Task ConsumeAsync(ChannelReader<DecryptionResult> reader, BufferScope scope, CancellationToken ct)
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
                    while (neededIndex - nextToWrite >= newWindow) newWindow *= 2;
                    var newRing = new DecryptionResult[newWindow];
                    var newFilled = new bool[newWindow];
                    var newSlotIndex = new long[newWindow];
                    for (int i = 0; i < window; i++)
                    {
                        if (!filled[i]) continue;
                        long idx = slotIndex[i];
                        int newSlot = (int)(idx % newWindow);
                        newRing[newSlot] = ring[i];
                        newFilled[newSlot] = true;
                        newSlotIndex[newSlot] = idx;
                    }
                    ring = newRing; filled = newFilled; slotIndex = newSlotIndex; window = newWindow;
                }

                async Task FlushReadyAsync()
                {
                    while (true)
                    {
                        int slot = (int)(nextToWrite % window);
                        if (filled[slot] && slotIndex[slot] == nextToWrite)
                        {
                            var res = ring[slot];
                            await _output.WriteAsync(res.Data.AsMemory(0, res.DataLength), ct).ConfigureAwait(false);
                            scope.Recycle(res.Data);
                            filled[slot] = false; nextToWrite++;
                        }
                        else break;
                    }
                }

                await foreach (var result in reader.ReadAllAsync(ct))
                {
                    if (result.Index == nextToWrite)
                    {
                        await _output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        scope.Recycle(result.Data);
                        nextToWrite++;
                        await FlushReadyAsync();
                    }
                    else
                    {
                        if (result.Index < nextToWrite)
                        {
                            continue;
                        }
                        EnsureCapacity(result.Index);
                        int slot = (int)(result.Index % window);
                        ring[slot] = result; slotIndex[slot] = result.Index; filled[slot] = true;
                    }
                }

                await FlushReadyAsync();
                for (int i = 0; i < window; i++)
                {
                    if (filled[i])
                    {
                        throw new InvalidDataException("Decryption output missing chunks. The encrypted data may be incomplete or corrupted.");
                    }
                }
            }, ct);
        }
    }
}
