using System.Buffers;
using System.Threading.Channels;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace Cotton.Crypto.Internals.Pipelines
{
    internal class DecryptionPipeline(Stream input, Stream output, byte[] fileKey, 
        uint noncePrefix, int threads, int keyId, int nonceSize, int tagSize, int maxChunkSize, ArrayPool<byte> pool)
    {
        public async Task RunAsync(CancellationToken ct)
        {
            var jobCh = Channel.CreateBounded<DecryptionJob>(new BoundedChannelOptions(threads * 4) { SingleWriter = true, SingleReader = false, FullMode = BoundedChannelFullMode.Wait });
            var resCh = Channel.CreateBounded<DecryptionResult>(new BoundedChannelOptions(threads * 4) { SingleWriter = false, SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

            var producer = ProduceAsync(jobCh.Writer, ct);
            var workers = StartWorkersAsync(jobCh.Reader, resCh.Writer, ct);
            var consumer = ConsumeAsync(resCh.Reader, ct);

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

        private Task ProduceAsync(ChannelWriter<DecryptionJob> writer, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                long idx = 0;
                try
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (input.CanSeek)
                        {
                            long bytesRemaining = input.Length - input.Position;
                            int minHeader = 4 + 4 + 8 + 4 + tagSize;
                            if (bytesRemaining == 0) break;
                            if (bytesRemaining < minHeader) throw new EndOfStreamException("Unexpected end of stream while reading chunk header.");
                        }
                        ChunkHeader chunkHeader;
                        try
                        {
                            chunkHeader = await AesGcmStreamFormat.ReadChunkHeaderAsync(input, tagSize, ct).ConfigureAwait(false);
                        }
                        catch (EndOfStreamException)
                        {
                            break;
                        }

                        if (chunkHeader.KeyId != keyId) throw new InvalidDataException("Chunk key ID does not match file key ID.");
                        if (chunkHeader.PlaintextLength <= 0 || chunkHeader.PlaintextLength > maxChunkSize) throw new InvalidDataException("Invalid chunk length in header.");
                        if (input.CanSeek)
                        {
                            long remaining = input.Length - input.Position;
                            if (remaining < chunkHeader.PlaintextLength) throw new EndOfStreamException("Unexpected end of stream while reading chunk ciphertext.");
                        }

                        int cipherLength = (int)chunkHeader.PlaintextLength;
                        byte[] cipher = pool.Rent(cipherLength);
                        await AesGcmStreamFormat.ReadExactlyAsync(input, cipher, cipherLength, ct).ConfigureAwait(false);
                        if (unchecked((ulong)idx) == ulong.MaxValue)
                        {
                            pool.Return(cipher, clearArray: false);
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

        private Task[] StartWorkersAsync(ChannelReader<DecryptionJob> reader, ChannelWriter<DecryptionResult> writer, CancellationToken ct)
        {
            var tasks = new Task[threads];
            for (int i = 0; i < threads; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using var gcm = new AesGcm(fileKey, tagSize);
                    byte[] nonceBuffer = new byte[nonceSize];
                    byte[] aad = new byte[32];
                    AesGcmStreamFormat.InitAadPrefix(aad, keyId);
                    await foreach (var job in reader.ReadAllAsync(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] plain = pool.Rent(job.DataLength);
                        try
                        {
                            AesGcmStreamFormat.ComposeNonce(nonceBuffer, noncePrefix, job.Index);
                            AesGcmStreamFormat.FillAadMutable(aad, job.Index, job.DataLength);
                            Tag128 tagCopy = job.Tag; Span<byte> tagSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref tagCopy, 1));
                            gcm.Decrypt(nonceBuffer, job.Cipher.AsSpan(0, job.DataLength), tagSpan, plain.AsSpan(0, job.DataLength), aad);
                            await writer.WriteAsync(new DecryptionResult(job.Index, plain, job.DataLength), ct).ConfigureAwait(false);
                        }
                        catch
                        {
                            pool.Return(plain, clearArray: false);
                            throw;
                        }
                        finally
                        {
                            pool.Return(job.Cipher, clearArray: false);
                        }
                    }
                }, ct);
            }
            return tasks;
        }

        private Task ConsumeAsync(ChannelReader<DecryptionResult> reader, CancellationToken ct)
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
                            await output.WriteAsync(res.Data.AsMemory(0, res.DataLength), ct).ConfigureAwait(false);
                            pool.Return(res.Data, clearArray: false);
                            filled[slot] = false; nextToWrite++;
                        }
                        else break;
                    }
                }

                await foreach (var result in reader.ReadAllAsync(ct))
                {
                    if (result.Index == nextToWrite)
                    {
                        await output.WriteAsync(result.Data.AsMemory(0, result.DataLength), ct).ConfigureAwait(false);
                        pool.Return(result.Data, clearArray: false);
                        nextToWrite++;
                        await FlushReadyAsync();
                    }
                    else
                    {
                        if (result.Index < nextToWrite)
                        {
                            pool.Return(result.Data, clearArray: false);
                            throw new InvalidDataException("Received duplicate or out-of-order chunk behind the write cursor.");
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
                        pool.Return(ring[i].Data, clearArray: false);
                        throw new InvalidDataException("Decryption output missing chunks. The encrypted data may be incomplete or corrupted.");
                    }
                }
            }, ct);
        }
    }
}
