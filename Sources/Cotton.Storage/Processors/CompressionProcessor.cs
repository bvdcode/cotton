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
            return Task.FromResult<Stream>(new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true));
        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {
            return Task.FromResult<Stream>(new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true));
        }
    }
}
