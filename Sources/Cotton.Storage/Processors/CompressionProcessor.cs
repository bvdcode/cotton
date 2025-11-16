using System.IO;
using System.IO.Compression;
using Cotton.Storage.Abstractions;

namespace Cotton.Storage.Processors
{
    public class CompressionProcessor : IStorageProcessor
    {
        public int Priority => 100;

        public Task<Stream> ReadAsync(string uid, Stream stream)
        {
            var brotli = new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true);
            return Task.FromResult<Stream>(brotli);
        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {
            var brotli = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true);
            return Task.FromResult<Stream>(brotli);
        }
    }
}
