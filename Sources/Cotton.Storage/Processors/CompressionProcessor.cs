using System.IO.Pipelines;
using K4os.Compression.LZ4;
using Cotton.Storage.Abstractions;
using K4os.Compression.LZ4.Streams;

namespace Cotton.Storage.Processors
{
    public class CompressionProcessor : IStorageProcessor
    {
        public int Priority => 100;

        public Task<Stream> ReadAsync(string uid, Stream stream)
        {
            // Decode: input is a readable compressed stream; return a readable decompressed stream
            ArgumentNullException.ThrowIfNull(stream);
            var decoded = LZ4Stream.Decode(stream, leaveOpen: false);
            return Task.FromResult<Stream>(decoded);
        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {
            // Encode: input is a readable plain stream; return a readable compressed stream via Pipe
            ArgumentNullException.ThrowIfNull(stream);
            if (!stream.CanRead)
            {
                throw new ArgumentException("Input stream must be readable.", nameof(stream));
            }

            var pipe = new Pipe(new PipeOptions(useSynchronizationContext: false));
            var readerStream = pipe.Reader.AsStream(leaveOpen: false);

            _ = Task.Run(async () =>
            {
                try
                {
                    await using var writerStream = pipe.Writer.AsStream(leaveOpen: true);
                    await using (var lz4 = LZ4Stream.Encode(writerStream, level: LZ4Level.L03_HC, leaveOpen: true))
                    {
                        await stream.CopyToAsync(lz4).ConfigureAwait(false);
                        await lz4.FlushAsync().ConfigureAwait(false);
                    } // ensure LZ4 writes footer before completing pipe

                    await pipe.Writer.CompleteAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await pipe.Writer.CompleteAsync(ex).ConfigureAwait(false);
                }
                finally
                {
                    // We own the input stream in the write pipeline step
                    try { stream.Dispose(); } catch { /* ignore */ }
                }
            });

            return Task.FromResult<Stream>(readerStream);
        }
    }
}
