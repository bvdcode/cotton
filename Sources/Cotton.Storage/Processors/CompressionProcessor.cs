using System.IO.Pipelines;
using System.IO.Compression;
using Cotton.Storage.Abstractions;

namespace Cotton.Storage.Processors
{
    public class CompressionProcessor : IStorageProcessor
    {
        public int Priority => 100;

        public Task<Stream> ReadAsync(string uid, Stream stream)
        {
            return Task.FromResult<Stream>(new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: false));
        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!stream.CanRead)
            {
                throw new ArgumentException("Input stream must be readable.", nameof(stream));
            }
            return Task.FromResult(stream);

            var pipe = new Pipe();
            var readerStream = pipe.Reader.AsStream(leaveOpen: false);

            _ = Task.Run(async () =>
            {
                try
                {
                    var writerStream = pipe.Writer.AsStream(leaveOpen: true);
                    await using (var brotli = new BrotliStream(writerStream, CompressionLevel.Fastest, leaveOpen: true))
                    {
                        if (stream.CanSeek)
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                        }
                        await stream.CopyToAsync(brotli).ConfigureAwait(false);
                    }
                    await pipe.Writer.CompleteAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await pipe.Writer.CompleteAsync(ex).ConfigureAwait(false);
                }
                finally
                {
                    try { stream.Dispose(); } catch { /* ignore */ }
                }
            });

            return Task.FromResult<Stream>(readerStream);
        }
    }
}
