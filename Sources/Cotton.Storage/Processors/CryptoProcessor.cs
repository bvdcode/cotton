using System.IO.Pipelines;
using Cotton.Crypto.Abstractions;
using Cotton.Storage.Abstractions;

namespace Cotton.Storage.Processors
{
    public class CryptoProcessor(IStreamCipher cipher) : IStorageProcessor
    {
        public int Priority => 100;

        public async Task<Stream> ReadAsync(string uid, Stream stream)
        {
            return stream;
        }

        public async Task<Stream> WriteAsync(string uid, Stream stream)
        {
            return stream;
        }
    }
}
