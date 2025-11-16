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
            var decoded = LZ4Stream.Decode(stream, leaveOpen: false);
            return Task.FromResult<Stream>(decoded);
        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {
            var encoded = LZ4Stream.Encode(stream, level: LZ4Level.L00_FAST, leaveOpen: false);
            return Task.FromResult<Stream>(encoded);
        }
    }
}
